using System;
using System.Buffers;
using System.Xml;

namespace OdfKit.Core;

/// <summary>
/// ODF 串流寫入熱迴圈的原始 XML 組裝器（ODS／ODT 等共用）。
/// 以固定大小的池化字元緩衝組裝「已知形狀」的標記，
/// 並透過 <see cref="XmlWriter.WriteRaw(char[], int, int)"/> 批次送入目標寫入器，
/// 免除 XmlWriter 逐呼叫的良構狀態機與命名空間解析成本，同時保留其
/// UTF-8 編碼、輸出緩衝與資料流順序（所有位元組仍流經同一個 XmlWriter，
/// 與 <c>WriteNode</c> 等直接使用 XmlWriter 的路徑交接時只需先呼叫
/// <see cref="FlushToTarget"/> 一個同步點）。
/// </summary>
/// <remarks>
/// 轉義行為與 XmlUtf8RawTextWriter（<c>NewLineHandling.Replace</c> 預設值）逐位元組一致：
/// 文字內容轉義 &amp;、&lt;、&gt;，並將 \r、\n、\r\n 一律改寫為 \r\n；
/// 屬性值另轉義引號（&amp;quot;）與 tab/LF/CR 的字元參照（&amp;#x9;、&amp;#xA;、&amp;#xD;）。
/// 呼叫端負責字元合法性（<see cref="OdfXmlCharacterGuard"/>）與標籤配對正確性；
/// 緩衝為固定大小（不隨列數成長），沖洗邊界絕不切開 UTF-16 代理對。
/// </remarks>
internal sealed class OdfRawXmlWriter : IDisposable
{
    private const int BufferCharCount = 16 * 1024;

    private readonly XmlWriter _target;
    private char[]? _buffer;
    private int _pos;
    private bool _startTagOpen;

    public OdfRawXmlWriter(XmlWriter target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _buffer = ArrayPool<char>.Shared.Rent(BufferCharCount);
    }

    /// <summary>
    /// 寫入元素起始標籤（「&lt;名稱」）並保持未閉合，等待後續屬性或內容；
    /// 與 XmlWriter 相同，若在閉合前即寫入結束標籤，會輸出自閉合形式。
    /// </summary>
    /// <param name="prefixedName">含前綴的元素名稱（例如 <c>table:table-cell</c> 或 <c>text:p</c>）。</param>
    public void WriteStartElement(string prefixedName)
    {
        CloseStartTag();
        AppendLiteral("<");
        AppendLiteral(prefixedName);
        _startTagOpen = true;
    }

    /// <summary>
    /// 寫入屬性（含屬性值轉義）。必須緊接在尚未閉合的起始標籤之後。
    /// </summary>
    /// <param name="prefixedName">含前綴的屬性名稱（例如 <c>office:value</c>）。</param>
    /// <param name="value">屬性值；將依 XmlWriter 相同規則轉義。</param>
    public void WriteAttribute(string prefixedName, ReadOnlySpan<char> value)
    {
        AppendLiteral(" ");
        AppendLiteral(prefixedName);
        AppendLiteral("=\"");
        AppendAttributeValue(value);
        AppendLiteral("\"");
    }

    /// <summary>
    /// 寫入結束標籤；若起始標籤尚未閉合則輸出自閉合「 /&gt;」（與 XmlWriter 輸出一致）。
    /// </summary>
    /// <param name="prefixedName">含前綴的元素名稱，需與對應起始標籤相同。</param>
    public void WriteEndElement(string prefixedName)
    {
        if (_startTagOpen)
        {
            AppendLiteral(" />");
            _startTagOpen = false;
            return;
        }

        AppendLiteral("</");
        AppendLiteral(prefixedName);
        AppendLiteral(">");
    }

    /// <summary>
    /// 寫入文字內容（含文字轉義與 \r、\n、\r\n 至 \r\n 的換行改寫）。
    /// </summary>
    /// <param name="value">文字內容。</param>
    public void WriteText(ReadOnlySpan<char> value)
    {
        CloseStartTag();
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            string replacement;
            int consumed = 1;
            switch (value[i])
            {
                case '&':
                    replacement = "&amp;";
                    break;
                case '<':
                    replacement = "&lt;";
                    break;
                case '>':
                    replacement = "&gt;";
                    break;
                case '\n':
                    replacement = "\r\n";
                    break;
                case '\r':
                    replacement = "\r\n";
                    if (i + 1 < value.Length && value[i + 1] == '\n')
                        consumed = 2;
                    break;
                default:
                    continue;
            }

            AppendRun(value.Slice(start, i - start));
            AppendLiteral(replacement);
            i += consumed - 1;
            start = i + 1;
        }

        AppendRun(value.Slice(start));
    }

    /// <summary>
    /// 閉合尚未完成的起始標籤（輸出「&gt;」）；若無未完成標籤則不動作。
    /// </summary>
    public void CloseStartTag()
    {
        if (_startTagOpen)
        {
            AppendLiteral(">");
            _startTagOpen = false;
        }
    }

    /// <summary>
    /// 與目標 XmlWriter 的交接同步點：閉合未完成起始標籤並將緩衝內容以
    /// <see cref="XmlWriter.WriteRaw(char[], int, int)"/> 送入目標，
    /// 之後呼叫端才可直接對目標 XmlWriter 寫入（例如 <c>WriteNode</c> 或文件收尾）。
    /// </summary>
    public void FlushToTarget()
    {
        CloseStartTag();
        if (_pos > 0 && _buffer is not null)
        {
            _target.WriteRaw(_buffer, 0, _pos);
            _pos = 0;
        }
    }

    /// <summary>
    /// 歸還池化緩衝。呼叫前應先完成 <see cref="FlushToTarget"/>。
    /// </summary>
    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

    private void AppendAttributeValue(ReadOnlySpan<char> value)
    {
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            string replacement;
            switch (value[i])
            {
                case '&':
                    replacement = "&amp;";
                    break;
                case '<':
                    replacement = "&lt;";
                    break;
                case '>':
                    replacement = "&gt;";
                    break;
                case '"':
                    replacement = "&quot;";
                    break;
                case '\t':
                    replacement = "&#x9;";
                    break;
                case '\n':
                    replacement = "&#xA;";
                    break;
                case '\r':
                    replacement = "&#xD;";
                    break;
                default:
                    continue;
            }

            AppendRun(value.Slice(start, i - start));
            AppendLiteral(replacement);
            start = i + 1;
        }

        AppendRun(value.Slice(start));
    }

    private void AppendLiteral(string text)
    {
        // 字面標記與實體皆為短 ASCII 字串，遠小於緩衝容量；空間不足時先沖洗即可。
        char[] buffer = _buffer!;
        if (_pos + text.Length > buffer.Length)
            FlushBuffer();
        text.AsSpan().CopyTo(buffer.AsSpan(_pos));
        _pos += text.Length;
    }

    private void AppendRun(ReadOnlySpan<char> run)
    {
        char[] buffer = _buffer!;
        while (!run.IsEmpty)
        {
            int free = buffer.Length - _pos;
            if (free == 0)
            {
                FlushBuffer();
                continue;
            }

            int take = run.Length < free ? run.Length : free;
            // 沖洗邊界不可切開代理對：XmlWriter.WriteRaw 各批次獨立進行 UTF-8 編碼，
            // 高代理若與其低代理分屬兩批會產生錯誤編碼。
            if (take < run.Length && char.IsHighSurrogate(run[take - 1]))
                take--;
            if (take == 0)
            {
                FlushBuffer();
                continue;
            }

            run.Slice(0, take).CopyTo(buffer.AsSpan(_pos));
            _pos += take;
            run = run.Slice(take);
        }
    }

    private void FlushBuffer()
    {
        char[] buffer = _buffer!;
        int len = _pos;
        if (len == 0)
            return;

        // 若緩衝尾端恰為高代理，保留至下一批，避免代理對被批次邊界切開。
        bool holdSurrogate = char.IsHighSurrogate(buffer[len - 1]);
        if (holdSurrogate)
            len--;
        if (len > 0)
            _target.WriteRaw(buffer, 0, len);
        if (holdSurrogate)
        {
            buffer[0] = buffer[_pos - 1];
            _pos = 1;
        }
        else
        {
            _pos = 0;
        }
    }
}
