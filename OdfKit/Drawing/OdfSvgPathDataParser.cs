using System;
using System.Globalization;
using System.Text;

namespace OdfKit.Drawing;

/// <summary>
/// Provides fast UTF-8 span-based parsing of SVG path data.
/// 提供 SVG path data 的 UTF-8 Span 快速解析工具。
/// </summary>
public static class OdfSvgPathDataParser
{
    /// <summary>
    /// Attempts to parse the coordinate bounds from SVG path data.
    /// 嘗試從 SVG path data 解析座標邊界。
    /// </summary>
    /// <param name="utf8PathData">The UTF-8 encoded SVG path data. / UTF-8 編碼的 SVG path data。</param>
    /// <param name="bounds">The parsed coordinate bounds. / 解析出的座標邊界。</param>
    /// <returns><see langword="true"/> if at least one coordinate was parsed successfully; otherwise <see langword="false"/>. / 若成功解析到至少一個座標則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public static bool TryGetBounds(ReadOnlySpan<byte> utf8PathData, out OdfSvgPathBounds bounds)
    {
        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        bool hasCoordinate = false;
        byte command = (byte)'M';
        int paramIndexInGroup = 0;
        int index = 0;

        while (index < utf8PathData.Length)
        {
            byte current = utf8PathData[index];
            if (IsAsciiLetter(current))
            {
                command = current;
                paramIndexInGroup = 0;
                index++;
                continue;
            }

            if (IsNumberStart(current))
            {
                int start = index;
                index++;
                while (index < utf8PathData.Length && IsNumberContinuation(utf8PathData, index))
                {
                    index++;
                }

                ReadOnlySpan<byte> token = utf8PathData.Slice(start, index - start);
                if (double.TryParse(
                    Encoding.ASCII.GetString(
#if NETSTANDARD2_0
                        token.ToArray()
#else
                        token
#endif
                    ),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value))
                {
                    byte upperCommand = ToUpperAscii(command);
                    int groupSize = upperCommand switch
                    {
                        (byte)'H' or (byte)'V' => 1,
                        (byte)'A' => 7,
                        (byte)'C' => 6,
                        (byte)'S' or (byte)'Q' => 4,
                        _ => 2
                    };

                    bool isXCoordinate = upperCommand switch
                    {
                        (byte)'H' => true,
                        (byte)'V' => false,
                        (byte)'A' => paramIndexInGroup == 5,
                        _ => paramIndexInGroup % 2 == 0
                    };
                    bool isYCoordinate = upperCommand switch
                    {
                        (byte)'H' => false,
                        (byte)'V' => true,
                        (byte)'A' => paramIndexInGroup == 6,
                        _ => paramIndexInGroup % 2 == 1
                    };

                    if (isXCoordinate || isYCoordinate)
                    {
                        hasCoordinate = true;
                        if (isXCoordinate)
                        {
                            minX = Math.Min(minX, value);
                            maxX = Math.Max(maxX, value);
                        }
                        else
                        {
                            minY = Math.Min(minY, value);
                            maxY = Math.Max(maxY, value);
                        }
                    }

                    paramIndexInGroup = (paramIndexInGroup + 1) % groupSize;
                }

                continue;
            }

            index++;
        }

        bounds = hasCoordinate
            ? new OdfSvgPathBounds(minX, minY, maxX, maxY)
            : default;
        return hasCoordinate;
    }

    private static bool IsAsciiLetter(byte value)
        => value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    private static bool IsNumberStart(byte value)
        => value is >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'+' or (byte)'.';

    private static bool IsNumberContinuation(ReadOnlySpan<byte> bytes, int index)
    {
        byte value = bytes[index];
        if (value is >= (byte)'0' and <= (byte)'9' or (byte)'.' or (byte)'e' or (byte)'E')
        {
            return true;
        }

        return (value is (byte)'-' or (byte)'+') &&
            index > 0 &&
            (bytes[index - 1] is (byte)'e' or (byte)'E');
    }

    private static byte ToUpperAscii(byte value)
        => value is >= (byte)'a' and <= (byte)'z'
            ? (byte)(value - 32)
            : value;
}

/// <summary>
/// Represents the coordinate bounds of SVG path data.
/// 表示 SVG path data 的座標邊界。
/// </summary>
public readonly struct OdfSvgPathBounds
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSvgPathBounds"/> struct.
    /// 初始化 <see cref="OdfSvgPathBounds"/> 結構的新執行個體。
    /// </summary>
    /// <param name="minX">The minimum X coordinate. / 最小 X 座標。</param>
    /// <param name="minY">The minimum Y coordinate. / 最小 Y 座標。</param>
    /// <param name="maxX">The maximum X coordinate. / 最大 X 座標。</param>
    /// <param name="maxY">The maximum Y coordinate. / 最大 Y 座標。</param>
    public OdfSvgPathBounds(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    /// <summary>
    /// Gets the minimum X coordinate.
    /// 取得最小 X 座標。
    /// </summary>
    public double MinX { get; }

    /// <summary>
    /// Gets the minimum Y coordinate.
    /// 取得最小 Y 座標。
    /// </summary>
    public double MinY { get; }

    /// <summary>
    /// Gets the maximum X coordinate.
    /// 取得最大 X 座標。
    /// </summary>
    public double MaxX { get; }

    /// <summary>
    /// Gets the maximum Y coordinate.
    /// 取得最大 Y 座標。
    /// </summary>
    public double MaxY { get; }

    /// <summary>
    /// Gets the width of the bounds.
    /// 取得邊界寬度。
    /// </summary>
    public double Width => MaxX - MinX;

    /// <summary>
    /// Gets the height of the bounds.
    /// 取得邊界高度。
    /// </summary>
    public double Height => MaxY - MinY;
}
