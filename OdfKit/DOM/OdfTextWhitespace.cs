using System;
using System.Globalization;
using System.Text;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// Encodes plain text into ODF paragraph content so that white space survives a conforming consumer.
/// 將純文字編碼為 ODF 段落內容，使空白字元在符合規範的消費端仍能保留。
/// </summary>
/// <remarks>
/// ODF 1.3 Part 1 §6.1.2 requires consumers to collapse white space in <c>text:p</c>, <c>text:h</c> and their
/// inline descendants: tab, carriage return and line feed characters become spaces, leading and trailing spaces
/// are removed and runs of spaces collapse to a single space. Only <c>text:s</c>, <c>text:tab</c> and
/// <c>text:line-break</c> carry significant white space, so literal white space must be written through them.
/// ODF 1.3 第 1 部分 §6.1.2 要求消費端折疊 <c>text:p</c>、<c>text:h</c> 及其行內子孫元素中的空白：定位字元、
/// 歸位與換行字元視為空格，前後空格被移除，連續空格折疊為單一空格。只有 <c>text:s</c>、<c>text:tab</c> 與
/// <c>text:line-break</c> 能保留有意義的空白，因此字面空白必須透過這些元素寫出。
/// </remarks>
internal static class OdfTextWhitespace
{
    /// <summary>
    /// Returns whether the text contains white space that a conforming consumer would otherwise collapse or drop.
    /// 傳回文字是否含有符合規範的消費端會折疊或丟棄的空白。
    /// </summary>
    internal static bool NeedsEncoding(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] == ' ' || text[text.Length - 1] == ' ')
        {
            return true;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\t' || ch == '\n' || ch == '\r')
            {
                return true;
            }

            if (ch == ' ' && i + 1 < text.Length && text[i + 1] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends the text to <paramref name="parent"/> as text nodes interleaved with <c>text:s</c>, <c>text:tab</c>
    /// and <c>text:line-break</c> elements.
    /// 將文字附加到 <paramref name="parent"/>：文字節點與 <c>text:s</c>、<c>text:tab</c>、<c>text:line-break</c> 元素交錯。
    /// </summary>
    /// <returns>Whether at least one line break was written. / 是否至少寫出一個換行。</returns>
    internal static bool AppendEncoded(OdfNode parent, string text)
    {
        bool wroteLineBreak = false;
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                parent.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = literal.ToString() });
                literal.Clear();
            }
        }

        void AppendSpaceElement(int count)
        {
            FlushLiteral();
            var node = new OdfNode(OdfNodeType.Element, "s", OdfNamespaces.Text, "text");
            if (count > 1)
            {
                node.SetAttribute("c", OdfNamespaces.Text, count.ToString(CultureInfo.InvariantCulture), "text");
            }

            parent.AppendChild(node);
        }

        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];
            if (ch == '\r' || ch == '\n')
            {
                i += ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                FlushLiteral();
                parent.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                wroteLineBreak = true;
            }
            else if (ch == '\t')
            {
                i++;
                FlushLiteral();
                parent.AppendChild(new OdfNode(OdfNodeType.Element, "tab", OdfNamespaces.Text, "text"));
            }
            else if (ch == ' ')
            {
                int runStart = i;
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }

                int count = i - runStart;
                if (runStart == 0 || i == text.Length)
                {
                    // 開頭與結尾的字面空格會被消費端移除，整段都必須以 text:s 表示。
                    AppendSpaceElement(count);
                }
                else
                {
                    literal.Append(' ');
                    if (count > 1)
                    {
                        AppendSpaceElement(count - 1);
                    }
                }
            }
            else
            {
                literal.Append(ch);
                i++;
            }
        }

        FlushLiteral();
        return wroteLineBreak;
    }
}
