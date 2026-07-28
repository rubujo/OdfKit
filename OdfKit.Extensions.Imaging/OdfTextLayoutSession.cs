using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HarfBuzzSharp;
using OdfKit.Styles;
using SkiaSharp;

namespace OdfKit.Extensions.Imaging;

/// <summary>
/// Provides a bounded, reusable HarfBuzz and Skia text-layout measurement session.
/// 提供具資源上限且可重用的 HarfBuzz 與 Skia 文字版面量測工作階段。
/// </summary>
/// <remarks>
/// Font files are loaded only from the supplied font context. Embedded or remote fonts are not
/// discovered automatically. Dispose the session after a batch operation to release native handles.
/// 字型檔只會由指定的字型情境載入，不會自動探索內嵌或遠端字型。批次作業完成後應釋放
/// 工作階段，以回收原生資源。
/// </remarks>
public sealed class OdfTextLayoutSession : IOdfTextLayoutMeasurer, IDisposable
{
    private const int DefaultMaximumFonts = 32;
    private const long DefaultMaximumFontBytes = 64L * 1024 * 1024;
    private const int DefaultMaximumMeasurementCacheEntries = 4_096;

    private readonly object _sync = new();
    private readonly OdfFontContext _fontContext;
    private readonly int _maximumFonts;
    private readonly long _maximumFontBytes;
    private readonly int _maximumMeasurementCacheEntries;
    private readonly Dictionary<FontKey, FontResource> _fonts = [];
    private readonly Queue<FontKey> _fontOrder = [];
    private readonly Dictionary<MeasurementKey, OdfTextMeasureResult> _measurements = [];
    private long _fontBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a bounded text-layout session using the process default font context.
    /// 使用處理程序預設字型情境初始化具資源上限的文字版面工作階段。
    /// </summary>
    public OdfTextLayoutSession()
        : this(
            OdfFontContext.Default,
            DefaultMaximumFonts,
            DefaultMaximumFontBytes,
            DefaultMaximumMeasurementCacheEntries)
    {
    }

    /// <summary>
    /// Initializes a bounded text-layout session using the specified font context.
    /// 使用指定的字型情境初始化具資源上限的文字版面工作階段。
    /// </summary>
    /// <param name="fontContext">The isolated font context. / 隔離的字型情境。</param>
    public OdfTextLayoutSession(OdfFontContext fontContext)
        : this(
            fontContext,
            DefaultMaximumFonts,
            DefaultMaximumFontBytes,
            DefaultMaximumMeasurementCacheEntries)
    {
    }

    /// <summary>
    /// Initializes a text-layout session with explicit resource limits.
    /// 使用明確的資源上限初始化文字版面工作階段。
    /// </summary>
    /// <param name="fontContext">The isolated font context. / 隔離的字型情境。</param>
    /// <param name="maximumFonts">The maximum cached font faces. / 最多快取的字型面數。</param>
    /// <param name="maximumFontBytes">The maximum total cached font bytes. / 最多快取的字型總位元組數。</param>
    /// <param name="maximumMeasurementCacheEntries">The maximum cached short-text measurements. / 最多快取的短文字量測數。</param>
    public OdfTextLayoutSession(
        OdfFontContext fontContext,
        int maximumFonts,
        long maximumFontBytes,
        int maximumMeasurementCacheEntries)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(
            fontContext,
            nameof(fontContext));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfLessThan(
            maximumFonts,
            1,
            nameof(maximumFonts));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfLessThan(
            maximumFontBytes,
            1,
            nameof(maximumFontBytes));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNegative(
            maximumMeasurementCacheEntries,
            nameof(maximumMeasurementCacheEntries));

        _fontContext = fontContext;
        _maximumFonts = maximumFonts;
        _maximumFontBytes = maximumFontBytes;
        _maximumMeasurementCacheEntries = maximumMeasurementCacheEntries;
    }

    /// <summary>
    /// Measures a styled text block using bounded native font resources.
    /// 使用具資源上限的原生字型資源量測具樣式文字區塊。
    /// </summary>
    /// <param name="request">The measurement request. / 量測要求。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The physical measurement result. / 實體量測結果。</returns>
    public OdfTextMeasureResult Measure(
        OdfTextMeasureRequest request,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(request, nameof(request));
        lock (_sync)
        {
            ThrowIfDisposed();
            if (request.MaximumTextElements < 1)
                throw new ArgumentOutOfRangeException(nameof(request));

            string text = request.Text ?? string.Empty;
            string fontFamily = string.IsNullOrWhiteSpace(request.FontFamily)
                ? OdfFontContext.DefaultBaseFontFamily
                : request.FontFamily;
            var key = new MeasurementKey(
                text,
                fontFamily,
                request.FontSizePoints,
                request.IsBold,
                request.IsItalic,
                request.WritingMode,
                request.AvailableWidthCentimeters,
                request.Wrap,
                request.RotationDegrees,
                request.MaximumTextElements);
            bool cacheable = text.Length <= 512 &&
                _maximumMeasurementCacheEntries > 0;
            if (cacheable &&
                _measurements.TryGetValue(
                    key,
                    out OdfTextMeasureResult cached))
            {
                return cached;
            }

            OdfTextMeasureResult measured = MeasureCore(
                request,
                cancellationToken);
            if (cacheable &&
                _measurements.Count < _maximumMeasurementCacheEntries)
            {
                _measurements[key] = measured;
            }
            return measured;
        }
    }

    /// <summary>
    /// Releases cached native font handles and managed font buffers.
    /// 釋放快取的原生字型控制代碼與受控字型緩衝區。
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (FontResource resource in _fonts.Values)
                resource.Dispose();
            _fonts.Clear();
            _fontOrder.Clear();
            _measurements.Clear();
            _fontBytes = 0;
        }
    }

    private OdfTextMeasureResult MeasureCore(
        OdfTextMeasureRequest request,
        CancellationToken cancellationToken)
    {
        double fontSize = IsFinite(request.FontSizePoints) &&
            request.FontSizePoints > 0
                ? Math.Min(request.FontSizePoints, 1_000)
                : 10;
        double? limit = request.Wrap &&
            request.AvailableWidthCentimeters is double available &&
            IsFinite(available) &&
            available > 0
                ? available
                : null;

        double maximumWidth = 0;
        double totalHeight = 0;
        int lineCount = 0;
        int textElements = 0;
        foreach (string line in EnumerateLines(request.Text ?? string.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            LineMeasurement natural = MeasureLine(
                line,
                request,
                fontSize,
                cancellationToken);
            if (limit is null || natural.WidthCentimeters <= limit.Value)
            {
                maximumWidth = Math.Max(maximumWidth, natural.WidthCentimeters);
                totalHeight += natural.HeightCentimeters;
                lineCount++;
                textElements += CountTextElements(
                    line,
                    request.MaximumTextElements - textElements,
                    cancellationToken);
                continue;
            }

            double currentWidth = 0;
            double currentHeight = natural.HeightCentimeters;
            TextElementEnumerator enumerator =
                StringInfo.GetTextElementEnumerator(line);
            while (enumerator.MoveNext())
            {
                if ((textElements++ & 0xff) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                if (textElements > request.MaximumTextElements)
                    throw new InvalidOperationException();

                string element = enumerator.GetTextElement();
                LineMeasurement elementSize = MeasureLine(
                    element,
                    request,
                    fontSize,
                    cancellationToken);
                if (currentWidth > 0 &&
                    currentWidth + elementSize.WidthCentimeters > limit.Value)
                {
                    maximumWidth = Math.Max(maximumWidth, currentWidth);
                    totalHeight += currentHeight;
                    lineCount++;
                    currentWidth = elementSize.WidthCentimeters;
                    currentHeight = elementSize.HeightCentimeters;
                }
                else
                {
                    currentWidth += elementSize.WidthCentimeters;
                    currentHeight = Math.Max(
                        currentHeight,
                        elementSize.HeightCentimeters);
                }
            }

            maximumWidth = Math.Max(maximumWidth, currentWidth);
            totalHeight += currentHeight;
            lineCount++;
        }

        if (lineCount == 0)
        {
            LineMeasurement empty = MeasureLine(
                string.Empty,
                request,
                fontSize,
                cancellationToken);
            totalHeight = empty.HeightCentimeters;
            lineCount = 1;
        }

        if (limit is not null)
            maximumWidth = Math.Min(maximumWidth, limit.Value);
        bool vertical = request.WritingMode is
            OdfWritingMode.TbLr or OdfWritingMode.TbRl;
        var result = vertical
            ? new OdfTextMeasureResult(
                totalHeight,
                maximumWidth,
                lineCount,
                true)
            : new OdfTextMeasureResult(
                maximumWidth,
                totalHeight,
                lineCount,
                true);
        return Rotate(result, request.RotationDegrees);
    }

    private LineMeasurement MeasureLine(
        string text,
        OdfTextMeasureRequest request,
        double fontSizePoints,
        CancellationToken cancellationToken)
    {
        double width = 0;
        double height = 0;
        string fontFamily = string.IsNullOrWhiteSpace(request.FontFamily)
            ? OdfFontContext.DefaultBaseFontFamily
            : request.FontFamily;
        List<(string Text, string FontName)> segments =
            _fontContext.SegmentText(text, fontFamily);
        if (segments.Count == 0)
        {
            FontResource emptyResource = GetFont(
                fontFamily,
                request.IsBold,
                request.IsItalic);
            return new LineMeasurement(
                0,
                emptyResource.GetLineHeightCentimeters(fontSizePoints));
        }

        foreach ((string segmentText, string fontName) in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FontResource resource = GetFont(
                fontName,
                request.IsBold,
                request.IsItalic);
            width += resource.MeasureWidthCentimeters(
                segmentText,
                fontSizePoints,
                request.WritingMode);
            height = Math.Max(
                height,
                resource.GetLineHeightCentimeters(fontSizePoints));
        }
        return new LineMeasurement(width, height);
    }

    private FontResource GetFont(
        string fontFamily,
        bool isBold,
        bool isItalic)
    {
        string mapped = _fontContext.MapFont(fontFamily);
        var key = new FontKey(mapped, isBold, isItalic);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_fonts.TryGetValue(key, out FontResource? cached))
                return cached;

            FontResource created = LoadFont(key);
            while (_fonts.Count >= _maximumFonts ||
                (_fontBytes + created.FontByteCount > _maximumFontBytes &&
                    _fonts.Count > 0))
            {
                FontKey oldest = _fontOrder.Dequeue();
                if (_fonts.TryGetValue(oldest, out FontResource? removed))
                {
                    _fonts.Remove(oldest);
                    _fontBytes -= removed.FontByteCount;
                    removed.Dispose();
                }
            }

            _fonts[key] = created;
            _fontOrder.Enqueue(key);
            _fontBytes += created.FontByteCount;
            return created;
        }
    }

    private FontResource LoadFont(FontKey key)
    {
        string? resolvedPath = _fontContext.ResolveFontPath(key.FontFamily);
        SKTypeface? typeface = null;
        bool ownsTypeface = false;
        byte[]? fontData = null;
        int faceIndex = 0;

        if (!string.IsNullOrEmpty(resolvedPath))
        {
            string fullPath = Path.GetFullPath(resolvedPath);
            try
            {
                fontData = ReadBoundedFile(fullPath, _maximumFontBytes);
                if (fontData.Length > 0)
                {
                    using SKData data = SKData.CreateCopy(fontData);
                    typeface = SKTypeface.FromData(data, faceIndex);
                    ownsTypeface = typeface is not null;
                }
            }
            catch (IOException)
            {
                fontData = null;
            }
            catch (UnauthorizedAccessException)
            {
                fontData = null;
            }
        }

        if (typeface is null)
        {
            var weight = key.IsBold
                ? SKFontStyleWeight.Bold
                : SKFontStyleWeight.Normal;
            var slant = key.IsItalic
                ? SKFontStyleSlant.Italic
                : SKFontStyleSlant.Upright;
            typeface = SKTypeface.FromFamilyName(
                key.FontFamily,
                new SKFontStyle(
                    (int)weight,
                    (int)SKFontStyleWidth.Normal,
                    slant));
            ownsTypeface = typeface is not null;
        }

        if (typeface is null)
        {
            typeface = SKTypeface.Default;
            ownsTypeface = false;
        }

        if (fontData is null)
        {
            using SKStreamAsset? stream = typeface.OpenStream(out faceIndex);
            if (stream is not null && stream.Length > 0 &&
                stream.Length <= _maximumFontBytes)
            {
                fontData = ReadBoundedStream(stream, _maximumFontBytes);
            }
        }

        return new FontResource(
            typeface,
            ownsTypeface,
            fontData,
            faceIndex);
    }

    private static byte[] ReadBoundedStream(
        SKStreamAsset stream,
        long maximumBytes)
    {
        using var output = new MemoryStream(
            (int)Math.Min(stream.Length, int.MaxValue));
        byte[] buffer = new byte[8192];
        while (true)
        {
            int read = stream.Read(buffer, buffer.Length);
            if (read == 0)
                break;
            if (output.Length + read > maximumBytes)
                return [];
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static byte[] ReadBoundedFile(string path, long maximumBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length <= 0 ||
            stream.Length > maximumBytes ||
            stream.Length > int.MaxValue)
        {
            return [];
        }

        int expectedLength = (int)stream.Length;
        byte[] buffer = new byte[expectedLength];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(
                buffer,
                totalRead,
                buffer.Length - totalRead);
            if (read == 0)
                break;
            totalRead += read;
        }
        if (totalRead == buffer.Length)
            return buffer;

        var truncated = new byte[totalRead];
        System.Buffer.BlockCopy(buffer, 0, truncated, 0, totalRead);
        return truncated;
    }

    private static int CountTextElements(
        string text,
        int remaining,
        CancellationToken cancellationToken)
    {
        int count = 0;
        TextElementEnumerator enumerator =
            StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if ((count++ & 0xff) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            if (count > remaining)
                throw new InvalidOperationException();
        }
        return count;
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] is not '\r' and not '\n')
                continue;
            yield return text.Substring(start, index - start);
            if (text[index] == '\r' &&
                index + 1 < text.Length &&
                text[index + 1] == '\n')
            {
                index++;
            }
            start = index + 1;
        }
        yield return text.Substring(start);
    }

    private static OdfTextMeasureResult Rotate(
        OdfTextMeasureResult result,
        double rotationDegrees)
    {
        if (!IsFinite(rotationDegrees))
            return result;
        double normalized = rotationDegrees % 360;
        if (Math.Abs(normalized) < 0.000001)
            return result;

        double radians = normalized * (Math.PI / 180);
        double cos = Math.Abs(Math.Cos(radians));
        double sin = Math.Abs(Math.Sin(radians));
        return new OdfTextMeasureResult(
            (result.WidthCentimeters * cos) +
                (result.HeightCentimeters * sin),
            (result.WidthCentimeters * sin) +
                (result.HeightCentimeters * cos),
            result.LineCount,
            true);
    }

    private void ThrowIfDisposed()
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(
            _disposed,
            nameof(OdfTextLayoutSession));
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private readonly record struct FontKey(
        string FontFamily,
        bool IsBold,
        bool IsItalic);

    private readonly record struct MeasurementKey(
        string Text,
        string FontFamily,
        double FontSizePoints,
        bool IsBold,
        bool IsItalic,
        OdfWritingMode WritingMode,
        double? AvailableWidthCentimeters,
        bool Wrap,
        double RotationDegrees,
        int MaximumTextElements);

    private readonly record struct LineMeasurement(
        double WidthCentimeters,
        double HeightCentimeters);

    private sealed class FontResource : IDisposable
    {
        private readonly SKTypeface _typeface;
        private readonly bool _ownsTypeface;
        private readonly byte[]? _fontData;
        private readonly int _faceIndex;

        internal FontResource(
            SKTypeface typeface,
            bool ownsTypeface,
            byte[]? fontData,
            int faceIndex)
        {
            _typeface = typeface;
            _ownsTypeface = ownsTypeface;
            _fontData = fontData is { Length: > 0 } ? fontData : null;
            _faceIndex = faceIndex;
        }

        internal long FontByteCount => _fontData?.LongLength ?? 0;

        internal double MeasureWidthCentimeters(
            string text,
            double fontSizePoints,
            OdfWritingMode writingMode)
        {
            if (text.Length == 0)
                return 0;

            if (_fontData is not null)
            {
                try
                {
                    GCHandle handle = GCHandle.Alloc(
                        _fontData,
                        GCHandleType.Pinned);
                    try
                    {
                        using var blob = new Blob(
                            handle.AddrOfPinnedObject(),
                            _fontData.Length,
                            MemoryMode.ReadOnly);
                        using var face = new Face(blob, _faceIndex);
                        using var font = new HarfBuzzSharp.Font(face);
                        int unitsPerEm = face.UnitsPerEm > 0
                            ? (int)face.UnitsPerEm
                            : 2_048;
                        font.SetScale(unitsPerEm, unitsPerEm);
                        using var buffer = new HarfBuzzSharp.Buffer();
                        buffer.AddUtf8(text);
                        buffer.GuessSegmentProperties();
                        buffer.Direction = writingMode switch
                        {
                            OdfWritingMode.RlTb => Direction.RightToLeft,
                            OdfWritingMode.TbRl or OdfWritingMode.TbLr =>
                                Direction.TopToBottom,
                            _ => Direction.LeftToRight
                        };
                        font.Shape(buffer);

                        long advance = 0;
                        foreach (GlyphPosition position in buffer.GlyphPositions)
                        {
                            advance += writingMode is
                                OdfWritingMode.TbRl or OdfWritingMode.TbLr
                                    ? Math.Abs(position.YAdvance)
                                    : Math.Abs(position.XAdvance);
                        }
                        double points = (advance / (double)unitsPerEm) *
                            fontSizePoints;
                        return points * (2.54 / 72);
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                catch (Exception)
                {
                    // 不可信或損毀的字型資料不得使批次版面作業失敗；
                    // 後續改採已建立之 Skia typeface 的受控量測路徑。
                }
            }

            using var fallbackFont = new SKFont(
                _typeface,
                (float)(fontSizePoints * (96.0 / 72.0)));
            float pixels = fallbackFont.MeasureText(text);
            return pixels * (2.54 / 96);
        }

        internal double GetLineHeightCentimeters(double fontSizePoints)
        {
            using var font = new SKFont(
                _typeface,
                (float)(fontSizePoints * (96.0 / 72.0)));
            SKFontMetrics metrics = font.Metrics;
            double pixels = Math.Max(
                metrics.Descent - metrics.Ascent + metrics.Leading,
                font.Size * 1.2);
            return pixels * (2.54 / 96);
        }

        public void Dispose()
        {
            if (_ownsTypeface)
                _typeface.Dispose();
        }
    }
}
