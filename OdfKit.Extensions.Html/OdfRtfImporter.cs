using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 RTF 匯入為 <see cref="TextDocument"/> 的 managed 淨室轉換器。
/// </summary>
public static class OdfRtfImporter
{
    /// <summary>
    /// 從 RTF 字串建立文字文件。
    /// </summary>
    /// <param name="rtf">來源 RTF 內容</param>
    /// <returns>轉換後的文字文件</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rtf"/> 為 null 時引發</exception>
    public static TextDocument Import(string rtf)
    {
        if (rtf is null)
            throw new ArgumentNullException(nameof(rtf));

        var parser = new Parser(rtf);
        return parser.Parse();
    }

    /// <summary>
    /// 從 RTF reader 建立文字文件。
    /// </summary>
    public static TextDocument Import(TextReader reader)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return Import(reader.ReadToEnd());
    }

    /// <summary>
    /// 從 RTF 檔案建立文字文件。
    /// </summary>
    public static TextDocument Load(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        using var reader = File.OpenText(path);
        return Import(reader);
    }

    private sealed class Parser
    {
        private const int MaxPictureHexChars = 50 * 1024 * 1024;
        private readonly string _rtf;
        private readonly TextDocument _document = TextDocument.Create();
        private readonly List<string> _colors = [];
        private readonly Stack<InlineStyle> _styles = new();
        private readonly StringBuilder _textBuffer = new();
        private readonly List<List<CellInfo>> _pendingTableRows = [];
        private readonly List<int> _currentCellX = [];
        private OdfParagraph? _paragraph;
        private OdfTextRun? _lastRun;
        private List<CellInfo>? _currentTableRow;
        private StringBuilder? _tableCellBuffer;
        private string? _pendingAnnotationAuthor;
        private string? _pendingAnnotationId;
        private string? _paragraphTextAlign;
        private string? _paragraphMarginLeft;
        private string? _paragraphMarginRight;
        private string? _paragraphMarginTop;
        private string? _paragraphMarginBottom;
        private string? _paragraphTextIndent;
        private string? _paragraphLineHeight;
        private bool _inTableCell;
        private int _unicodeFallbackLength = 1;
        private InlineStyle _style;

        public Parser(string rtf)
        {
            _rtf = rtf;
        }

        public TextDocument Parse()
        {
            _styles.Push(InlineStyle.Empty);
            for (int i = 0; i < _rtf.Length;)
            {
                char c = _rtf[i];
                if (c == '{')
                {
                    if (StartsGroup(i, "fonttbl"))
                    {
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsGroup(i, "colortbl"))
                    {
                        ReadColorTable(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsGroup(i, "info"))
                    {
                        ReadInfoGroup(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsGroup(i, "field"))
                    {
                        FlushText();
                        ReadHyperlinkGroup(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsGroup(i, "footnote"))
                    {
                        FlushText();
                        ReadFootnoteGroup(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsGroup(i, "pict"))
                    {
                        FlushText();
                        ReadPictureGroup(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsDestinationGroup(i, "atnauthor"))
                    {
                        FlushText();
                        ReadAnnotationMetadataGroup(i, "atnauthor");
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsDestinationGroup(i, "atnid"))
                    {
                        FlushText();
                        ReadAnnotationMetadataGroup(i, "atnid");
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    if (StartsDestinationGroup(i, "annotation"))
                    {
                        FlushText();
                        ReadAnnotationGroup(i);
                        i = FindGroupEnd(i) + 1;
                        continue;
                    }

                    FlushText();
                    _styles.Push(_style);
                    i++;
                }
                else if (c == '}')
                {
                    FlushText();
                    _style = _styles.Count > 0 ? _styles.Pop() : InlineStyle.Empty;
                    i++;
                }
                else if (c == '\\')
                {
                    FlushText();
                    i = ReadControl(i);
                }
                else
                {
                    _textBuffer.Append(c);
                    i++;
                }
            }

            FlushText();
            FlushPendingTable();
            return _document;
        }

        private int ReadControl(int index)
        {
            if (index + 1 >= _rtf.Length)
            {
                return index + 1;
            }

            char next = _rtf[index + 1];
            if (next is '\\' or '{' or '}')
            {
                _textBuffer.Append(next);
                return index + 2;
            }

            if (next == '\'' && TryDecodeAnsiHexEscape(_rtf, index, out char decoded))
            {
                _textBuffer.Append(decoded);
                return index + 4;
            }

            switch (next)
            {
                case '~':
                    _textBuffer.Append('\u00A0');
                    return index + 2;
                case '-':
                    _textBuffer.Append('\u00AD');
                    return index + 2;
                case '_':
                    _textBuffer.Append('\u2011');
                    return index + 2;
            }

            if (!char.IsLetter(next))
            {
                return index + 2;
            }

            int nameStart = index + 1;
            int pos = nameStart;
            while (pos < _rtf.Length && char.IsLetter(_rtf[pos]))
            {
                pos++;
            }

            string name = _rtf.Substring(nameStart, pos - nameStart);
            bool negative = pos < _rtf.Length && _rtf[pos] == '-';
            if (negative)
            {
                pos++;
            }

            int numberStart = pos;
            while (pos < _rtf.Length && char.IsDigit(_rtf[pos]))
            {
                pos++;
            }

            int? number = null;
            if (pos > numberStart &&
                int.TryParse(_rtf.Substring(numberStart, pos - numberStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                number = negative ? -parsed : parsed;
            }

            if (pos < _rtf.Length && _rtf[pos] == ' ')
            {
                pos++;
            }

            if (string.Equals(name, "u", StringComparison.Ordinal))
            {
                AppendUnicodeEscape(number);
                return SkipUnicodeFallback(_rtf, pos, _unicodeFallbackLength);
            }

            ApplyControl(name, number);
            return pos;
        }

        private void ApplyControl(string name, int? number)
        {
            switch (name)
            {
                case "par":
                    _paragraph = null;
                    _lastRun = null;
                    break;
                case "pard":
                    _paragraphTextAlign = null;
                    _paragraphMarginLeft = null;
                    _paragraphMarginRight = null;
                    _paragraphMarginTop = null;
                    _paragraphMarginBottom = null;
                    _paragraphTextIndent = null;
                    _paragraphLineHeight = null;
                    break;
                case "ql":
                    SetParagraphTextAlign("left");
                    break;
                case "qc":
                    SetParagraphTextAlign("center");
                    break;
                case "qr":
                    SetParagraphTextAlign("right");
                    break;
                case "qj":
                    SetParagraphTextAlign("justify");
                    break;
                case "li":
                    SetParagraphMarginLeft(number);
                    break;
                case "ri":
                    SetParagraphMarginRight(number);
                    break;
                case "fi":
                    SetParagraphTextIndent(number);
                    break;
                case "sb":
                    SetParagraphMarginTop(number);
                    break;
                case "sa":
                    SetParagraphMarginBottom(number);
                    break;

                case "sl":
                    SetParagraphLineHeight(number);
                    break;
                case "slmult":
                    break;
                case "uc":
                    _unicodeFallbackLength = Math.Max(0, number ?? 1);
                    break;
                case "page":
                    EnsureParagraph().AddSoftPageBreak();
                    _lastRun = null;
                    break;
                case "trowd":
                    StartTableRow();
                    break;
                case "intbl":
                    EnsureTableCell();
                    break;
                case "cell":
                    FinishTableCell();
                    break;
                case "row":
                    FinishTableRow();
                    break;
                case "cellx":
                    if (number.HasValue)
                    {
                        _currentCellX.Add(number.Value);
                    }
                    break;
                case "atnstart":
                    if (number.HasValue)
                    {
                        var startNode = new OdfNode(OdfNodeType.Element, "annotation-start", OdfNamespaces.Office, "office");
                        startNode.SetAttribute("name", OdfNamespaces.Office, "comment-" + number.Value, "office");
                        EnsureParagraph().Node.AppendChild(startNode);
                    }
                    break;
                case "atnend":
                    if (number.HasValue)
                    {
                        var endNode = new OdfNode(OdfNodeType.Element, "annotation-end", OdfNamespaces.Office, "office");
                        endNode.SetAttribute("name", OdfNamespaces.Office, "comment-" + number.Value, "office");
                        EnsureParagraph().Node.AppendChild(endNode);
                    }
                    break;
                case "trautofit":
                    break;
                case "line":
                    _textBuffer.Append('\n');
                    break;
                case "tab":
                    _textBuffer.Append('\t');
                    break;
                case "bullet":
                    _textBuffer.Append("•");
                    break;
                case "enspace":
                    _textBuffer.Append('\u2002');
                    break;
                case "emspace":
                    _textBuffer.Append('\u2003');
                    break;
                case "qmspace":
                    _textBuffer.Append('\u2005');
                    break;
                case "emdash":
                    _textBuffer.Append('\u2014');
                    break;
                case "endash":
                    _textBuffer.Append('\u2013');
                    break;
                case "lquote":
                    _textBuffer.Append('\u2018');
                    break;
                case "rquote":
                    _textBuffer.Append('\u2019');
                    break;
                case "ldblquote":
                    _textBuffer.Append('\u201C');
                    break;
                case "rdblquote":
                    _textBuffer.Append('\u201D');
                    break;
                case "plain":
                    _style = InlineStyle.Empty;
                    break;
                case "b":
                    _style = _style with { Bold = number != 0 };
                    break;
                case "i":
                    _style = _style with { Italic = number != 0 };
                    break;
                case "ul":
                    _style = _style with { Underline = true };
                    break;
                case "ulnone":
                    _style = _style with { Underline = false };
                    break;
                case "strike":
                    _style = _style with { Strikethrough = number != 0 };
                    break;
                case "super":
                    _style = _style with { TextPosition = "super" };
                    break;
                case "sub":
                    _style = _style with { TextPosition = "sub" };
                    break;
                case "nosupersub":
                    _style = _style with { TextPosition = null };
                    break;
                case "fs":
                    _style = _style with { FontSize = number.HasValue ? FormatHalfPoints(number.Value) : null };
                    break;
                case "cf":
                    _style = _style with { Color = ReadColor(number) };
                    break;
            }
        }

        private void AppendUnicodeEscape(int? number)
        {
            if (number.HasValue)
            {
                _textBuffer.Append(DecodeSignedUnicodeEscape(number.Value));
            }
        }

        private void FlushText()
        {
            if (_textBuffer.Length == 0)
            {
                return;
            }

            if (_inTableCell)
            {
                EnsureTableCell();
                _tableCellBuffer!.Append(_textBuffer);
                _textBuffer.Clear();
                return;
            }

            AppendParagraphText(_textBuffer.ToString(), _style);
            _textBuffer.Clear();
        }

        private void AppendParagraphText(string text, InlineStyle style)
        {
            int index = 0;
            while (index < text.Length)
            {
                int marker = text.IndexOf('[', index);
                if (marker < 0)
                {
                    AppendStyledText(text.Substring(index), style);
                    return;
                }

                AppendStyledText(text.Substring(index, marker - index), style);
                if (TryAppendImageMarker(text, marker, out int nextIndex) ||
                    TryAppendCommentMarker(text, marker, out nextIndex))
                {
                    index = nextIndex;
                    continue;
                }

                AppendStyledText(text.Substring(marker, 1), style);
                index = marker + 1;
            }
        }

        private void AppendStyledText(string text, InlineStyle style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            OdfTextRun run = EnsureParagraph().AddTextRun(text);
            ApplyStyle(run, style);
            _lastRun = run;
        }

        private bool TryAppendImageMarker(string text, int marker, out int nextIndex)
        {
            nextIndex = marker;
            const string prefix = "[Image: ";
            if (!StartsWith(text, marker, prefix))
            {
                return false;
            }

            int end = text.IndexOf(']', marker + prefix.Length);
            if (end < 0)
            {
                return false;
            }

            string body = text.Substring(marker + prefix.Length, end - marker - prefix.Length);
            string altText = body;
            string href = string.Empty;
            int hrefStart = body.LastIndexOf(" (", StringComparison.Ordinal);
            if (hrefStart >= 0 && body.EndsWith(")", StringComparison.Ordinal))
            {
                altText = body.Substring(0, hrefStart);
                href = body.Substring(hrefStart + 2, body.Length - hrefStart - 3);
            }

            if (string.IsNullOrWhiteSpace(altText))
            {
                altText = "image";
            }

            OdfImage image = EnsureParagraph().AddImage(href, OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), altText);
            image.AltText = altText;
            _lastRun = null;
            nextIndex = end + 1;
            return true;
        }

        private bool TryAppendCommentMarker(string text, int marker, out int nextIndex)
        {
            nextIndex = marker;
            const string prefix = "[Comment";
            if (!StartsWith(text, marker, prefix))
            {
                return false;
            }

            int end = text.IndexOf(']', marker + prefix.Length);
            if (end < 0)
            {
                return false;
            }

            string body = text.Substring(marker + 1, end - marker - 1);
            if (!TryReadCommentMarkerBody(body, out string? author, out string? commentText))
            {
                return false;
            }

            bool exists = FindChild(EnsureParagraph().Node, n =>
                n.NodeType == OdfNodeType.Element &&
                n.NamespaceUri == OdfNamespaces.Office &&
                n.LocalName == "annotation" &&
                FindChild(n, c => c.NodeType == OdfNodeType.Element && c.NamespaceUri == OdfNamespaces.Dc && c.LocalName == "creator")?.TextContent == author &&
                FindChild(n, c => c.NodeType == OdfNodeType.Element && c.NamespaceUri == OdfNamespaces.Text && c.LocalName == "p")?.TextContent == commentText) is not null;

            if (!exists)
            {
                EnsureParagraph().AddComment(new OdfComment(author!, commentText!));
            }
            _lastRun = null;
            nextIndex = end + 1;
            return true;
        }

        private static bool TryReadCommentMarkerBody(string body, out string? author, out string? commentText)
        {
            const string commentByPrefix = "Comment by ";
            const string commentPrefix = "Comment: ";
            if (body.StartsWith(commentByPrefix, StringComparison.Ordinal))
            {
                int separator = body.IndexOf(": ", commentByPrefix.Length, StringComparison.Ordinal);
                if (separator > commentByPrefix.Length)
                {
                    author = body.Substring(commentByPrefix.Length, separator - commentByPrefix.Length);
                    commentText = body.Substring(separator + 2);
                    return true;
                }
            }
            else if (body.StartsWith(commentPrefix, StringComparison.Ordinal))
            {
                author = string.Empty;
                commentText = body.Substring(commentPrefix.Length);
                return true;
            }

            author = null;
            commentText = null;
            return false;
        }

        private OdfParagraph EnsureParagraph()
        {
            FlushPendingTable();
            if (_paragraph is null)
            {
                _paragraph = _document.AddParagraph();
                ApplyParagraphFormatting();
            }

            return _paragraph;
        }

        private void SetParagraphTextAlign(string textAlign)
        {
            _paragraphTextAlign = textAlign;
            ApplyParagraphFormatting();
        }

        private void SetParagraphMarginLeft(int? twips)
        {
            _paragraphMarginLeft = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void SetParagraphMarginRight(int? twips)
        {
            _paragraphMarginRight = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void SetParagraphTextIndent(int? twips)
        {
            _paragraphTextIndent = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void SetParagraphMarginTop(int? twips)
        {
            _paragraphMarginTop = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void SetParagraphMarginBottom(int? twips)
        {
            _paragraphMarginBottom = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void SetParagraphLineHeight(int? twips)
        {
            _paragraphLineHeight = FormatTwipsAsPoints(twips);
            ApplyParagraphFormatting();
        }

        private void ApplyParagraphFormatting()
        {
            if (_paragraph is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphTextAlign))
            {
                _paragraph.HorizontalAlignment = _paragraphTextAlign;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphMarginLeft))
            {
                _paragraph.Style.MarginLeft = _paragraphMarginLeft;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphMarginRight))
            {
                _paragraph.Style.MarginRight = _paragraphMarginRight;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphMarginTop))
            {
                _paragraph.Style.MarginTop = _paragraphMarginTop;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphMarginBottom))
            {
                _paragraph.Style.MarginBottom = _paragraphMarginBottom;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphTextIndent))
            {
                _paragraph.TextIndent = _paragraphTextIndent;
            }

            if (!string.IsNullOrWhiteSpace(_paragraphLineHeight))
            {
                _paragraph.Style.LineSpacing = _paragraphLineHeight;
            }
        }

        private static string? FormatTwipsAsPoints(int? twips) =>
            twips.HasValue ? OdfLength.FromPoints(twips.Value / 20d).ToString() : null;

        private void StartTableRow()
        {
            _paragraph = null;
            _lastRun = null;
            _currentTableRow = [];
            _tableCellBuffer = null;
            _inTableCell = false;
            _currentCellX.Clear();
        }

        private void EnsureTableCell()
        {
            _currentTableRow ??= [];
            _tableCellBuffer ??= new StringBuilder();
            _inTableCell = true;
        }

        private void FinishTableCell()
        {
            if (_currentTableRow is null && _tableCellBuffer is null)
            {
                return;
            }

            _currentTableRow ??= [];
            string text = _tableCellBuffer?.ToString().TrimEnd('\r', '\n') ?? string.Empty;

            int cellIdx = _currentTableRow.Count;
            int rightBoundary;
            if (cellIdx < _currentCellX.Count)
            {
                rightBoundary = _currentCellX[cellIdx];
            }
            else
            {
                int prev = cellIdx > 0 ? _currentTableRow[cellIdx - 1].RightBoundary : 0;
                rightBoundary = prev + 2000;
            }

            _currentTableRow.Add(new CellInfo(text, rightBoundary));
            _tableCellBuffer = null;
            _inTableCell = false;
        }

        private void FinishTableRow()
        {
            if (_inTableCell || _tableCellBuffer is not null)
            {
                FinishTableCell();
            }

            if (_currentTableRow is { Count: > 0 })
            {
                _pendingTableRows.Add(_currentTableRow);
            }

            _currentTableRow = null;
            _tableCellBuffer = null;
            _inTableCell = false;
        }

        private void FlushPendingTable()
        {
            if (_currentTableRow is not null)
            {
                FinishTableRow();
            }

            if (_pendingTableRows.Count == 0)
            {
                return;
            }

            var gridSet = new SortedSet<int> { 0 };
            foreach (List<CellInfo> row in _pendingTableRows)
            {
                foreach (CellInfo cell in row)
                {
                    gridSet.Add(cell.RightBoundary);
                }
            }

            var grid = new List<int>(gridSet);
            int columns = grid.Count - 1;

            if (columns <= 0)
            {
                _pendingTableRows.Clear();
                return;
            }

            OdfTable table = _document.AddTable(_pendingTableRows.Count, columns);
            for (int r = 0; r < _pendingTableRows.Count; r++)
            {
                List<CellInfo> row = _pendingTableRows[r];
                int left = 0;
                for (int c = 0; c < row.Count; c++)
                {
                    CellInfo cell = row[c];
                    int right = cell.RightBoundary;

                    int startCol = grid.IndexOf(left);
                    int endCol = grid.IndexOf(right);

                    if (startCol < 0)
                        startCol = 0;
                    if (endCol < 0)
                        endCol = startCol + 1;
                    if (endCol > columns)
                        endCol = columns;

                    if (endCol > startCol)
                    {
                        int colSpan = endCol - startCol;
                        table.GetCell(r, startCol).AddParagraph(cell.Text);

                        if (colSpan > 1)
                        {
                            table.MergeCells(r, startCol, 1, colSpan);
                        }
                    }

                    left = right;
                }
            }

            _pendingTableRows.Clear();
            _paragraph = null;
            _lastRun = null;
        }

        private void ApplyStyle(OdfTextRun run, InlineStyle style)
        {
            if (style.Bold)
            {
                run.IsBold = true;
            }

            if (style.Italic)
            {
                run.IsItalic = true;
            }

            if (style.Underline)
            {
                run.IsUnderline = true;
            }

            if (style.Strikethrough)
            {
                run.IsStrikethrough = true;
            }

            if (!string.IsNullOrWhiteSpace(style.TextPosition))
            {
                run.TextPosition = style.TextPosition;
            }

            if (!string.IsNullOrWhiteSpace(style.FontSize))
            {
                run.SetFontSize(style.FontSize!);
            }

            if (!string.IsNullOrWhiteSpace(style.Color))
            {
                run.Color = style.Color;
            }
        }

        private void ReadFootnoteGroup(int groupStart)
        {
            string text = DecodeGroupText(groupStart, skipFirstControl: true).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int split = text.IndexOf(' ');
            string citation = split > 0 ? text.Substring(0, split) : text;
            string body = split > 0 ? text.Substring(split + 1) : string.Empty;
            RemoveCitationSuffix(citation);
            EnsureParagraph().AddFootnote(citation, body);
            _lastRun = null;
        }

        private void RemoveCitationSuffix(string citation)
        {
            if (_lastRun is null ||
                string.IsNullOrEmpty(citation) ||
                !_lastRun.Text.EndsWith(citation, StringComparison.Ordinal))
            {
                return;
            }

            _lastRun.Text = _lastRun.Text.Substring(0, _lastRun.Text.Length - citation.Length);
        }

        private void ReadHyperlinkGroup(int groupStart)
        {
            int groupEnd = FindGroupEnd(groupStart);
            string group = _rtf.Substring(groupStart, groupEnd - groupStart + 1);
            string? url = ReadHyperlinkUrl(group);
            string? result = ReadFieldResult(group);
            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrEmpty(result))
            {
                EnsureParagraph().AddHyperlink(url!, result!);
            }
        }

        private void ReadAnnotationMetadataGroup(int groupStart, string controlWord)
        {
            string text = DecodeDestinationGroupText(groupStart, controlWord).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.Equals(controlWord, "atnauthor", StringComparison.Ordinal))
            {
                _pendingAnnotationAuthor = text;
            }
            else if (string.Equals(controlWord, "atnid", StringComparison.Ordinal))
            {
                _pendingAnnotationId = text;
            }
        }

        private void ReadAnnotationGroup(int groupStart)
        {
            string text = DecodeDestinationGroupText(groupStart, "annotation").Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string name = _pendingAnnotationId ?? "comment-1";
            if (!name.StartsWith("comment-", StringComparison.Ordinal) && int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                name = "comment-" + name;
            }



            OdfComment comment = new OdfComment(_pendingAnnotationAuthor ?? string.Empty, text, DateTime.UtcNow, name);
            OdfNode annoNode = comment.ToXmlNode();

            OdfNode? startNode = FindChild(EnsureParagraph().Node, n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Office && n.LocalName == "annotation-start" && n.GetAttribute("name", OdfNamespaces.Office) == name);
            if (startNode is not null)
            {
                EnsureParagraph().Node.InsertAfter(annoNode, startNode);
            }
            else
            {
                EnsureParagraph().Node.AppendChild(annoNode);
            }

            _pendingAnnotationAuthor = null;
            _pendingAnnotationId = null;
            _lastRun = null;
        }

        private static OdfNode? FindChild(OdfNode parent, Func<OdfNode, bool> predicate)
        {
            foreach (OdfNode child in parent.Children)
            {
                if (predicate(child))
                    return child;

                OdfNode? found = FindChild(child, predicate);
                if (found is not null)
                    return found;
            }

            return null;
        }

        private void ReadInfoGroup(int groupStart)
        {
            int end = FindGroupEnd(groupStart);
            string groupText = _rtf.Substring(groupStart, end - groupStart + 1);

            string title = ExtractRtfGroupText(groupText, "title");
            string author = ExtractRtfGroupText(groupText, "author");
            string subject = ExtractRtfGroupText(groupText, "subject");
            string doccomm = ExtractRtfGroupText(groupText, "doccomm");
            string comment = ExtractRtfGroupText(groupText, "comment");

            if (!string.IsNullOrEmpty(title))
                _document.Metadata.Title = title;
            if (!string.IsNullOrEmpty(author))
                _document.Metadata.Creator = author;
            if (!string.IsNullOrEmpty(subject))
                _document.Metadata.Subject = subject;
            if (!string.IsNullOrEmpty(doccomm))
                _document.Metadata.Description = doccomm;
            if (!string.IsNullOrEmpty(comment) && comment.StartsWith("language:", StringComparison.OrdinalIgnoreCase))
            {
                _document.Metadata.Language = comment.Substring("language:".Length).Trim();
            }
        }

        private static string ExtractRtfGroupText(string source, string name)
        {
            string target1 = "{\\" + name + " ";
            string target2 = "{\\*\\" + name + " ";
            int idx = source.IndexOf(target1, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                idx = source.IndexOf(target2, StringComparison.OrdinalIgnoreCase);
            }

            if (idx < 0)
            {
                return string.Empty;
            }

            int start = idx + (source[idx + 2] == '*' ? target2.Length : target1.Length);
            int bracketCount = 1;
            var sb = new StringBuilder();
            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    bracketCount++;
                }
                else if (c == '}')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        break;
                    }
                }
                sb.Append(c);
            }

            return UnescapeRtfText(sb.ToString().Trim());
        }

        private static string UnescapeRtfText(string rtf)
        {
            var sb = new StringBuilder();
            int unicodeFallbackLength = 1;
            for (int i = 0; i < rtf.Length;)
            {
                char c = rtf[i];
                if (c == '\\')
                {
                    if (i + 1 >= rtf.Length)
                    {
                        i++;
                        continue;
                    }
                    char next = rtf[i + 1];
                    if (next is '\\' or '{' or '}')
                    {
                        sb.Append(next);
                        i += 2;
                    }
                    else if (next == '\'' && TryDecodeAnsiHexEscape(rtf, i, out char decoded))
                    {
                        sb.Append(decoded);
                        i += 4;
                    }
                    else if (StartsWith(rtf, i, "\\uc"))
                    {
                        i = ReadUnicodeFallbackLength(rtf, i, out unicodeFallbackLength);
                    }
                    else if (StartsWith(rtf, i, "\\u"))
                    {
                        i = ReadUnicodeEscape(rtf, i, sb, unicodeFallbackLength);
                    }
                    else
                    {
                        if (StartsWith(rtf, i, "\\line"))
                        {
                            sb.Append('\n');
                            i = SkipControl(rtf, i);
                        }
                        else if (StartsWith(rtf, i, "\\tab"))
                        {
                            sb.Append('\t');
                            i = SkipControl(rtf, i);
                        }
                        else
                        {
                            i = SkipControl(rtf, i);
                        }
                    }
                }
                else if (c is '{' or '}')
                {
                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        private void ReadPictureGroup(int groupStart)
        {
            int groupEnd = FindGroupEnd(groupStart);
            string group = _rtf.Substring(groupStart, groupEnd - groupStart + 1);
            if (!TryReadPictureBytes(group, out byte[]? bytes, out string? preferredName, out OdfLength width, out OdfLength height))
            {
                return;
            }

            OdfImage image = _document.Body.Images.Add(
                bytes!,
                width,
                height,
                preferredName);
            image.AltText = "RTF image";
            _paragraph = null;
            _lastRun = null;
        }

        private static bool TryReadPictureBytes(
            string group,
            out byte[]? bytes,
            out string? preferredName,
            out OdfLength width,
            out OdfLength height)
        {
            bytes = null;
            preferredName = null;
            width = ReadPictureLength(group, "picwgoal", OdfLength.FromCentimeters(4));
            height = ReadPictureLength(group, "pichgoal", OdfLength.FromCentimeters(3));
            string extension = group.IndexOf(@"\jpegblip", StringComparison.OrdinalIgnoreCase) >= 0
                ? ".jpg"
                : group.IndexOf(@"\pngblip", StringComparison.OrdinalIgnoreCase) >= 0
                    ? ".png"
                    : string.Empty;
            if (extension.Length == 0)
            {
                return false;
            }

            var hex = new StringBuilder(Math.Min(group.Length, 8192));
            for (int i = 0; i < group.Length; i++)
            {
                char c = group[i];
                if (IsHexDigit(c))
                {
                    if (hex.Length >= MaxPictureHexChars)
                    {
                        return false;
                    }

                    hex.Append(c);
                }
                else if (c == '\\')
                {
                    i = SkipControl(group, i) - 1;
                }
            }

            if (hex.Length == 0 || hex.Length % 2 != 0)
            {
                return false;
            }

            byte[] data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((HexValue(hex[i * 2]) << 4) | HexValue(hex[(i * 2) + 1]));
            }

            bytes = data;
            preferredName = "rtf-image" + extension;
            return true;
        }

        private static OdfLength ReadPictureLength(string group, string controlName, OdfLength fallback)
        {
            if (!TryReadPictureControlNumber(group, controlName, out int twips) || twips <= 0)
            {
                return fallback;
            }

            return OdfLength.FromPoints(twips / 20d);
        }

        private static bool TryReadPictureControlNumber(string group, string controlName, out int value)
        {
            value = 0;
            string marker = "\\" + controlName;
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != '\\' || !StartsWith(group, i, marker))
                {
                    continue;
                }

                int pos = i + marker.Length;
                bool negative = pos < group.Length && group[pos] == '-';
                if (negative)
                {
                    pos++;
                }

                int start = pos;
                while (pos < group.Length && char.IsDigit(group[pos]))
                {
                    pos++;
                }

                return pos > start &&
                    int.TryParse(group.Substring(start, pos - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                    !negative;
            }

            return false;
        }

        private static bool IsHexDigit(char c) =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

        private static int HexValue(char c) =>
            c <= '9' ? c - '0' : (c <= 'F' ? c - 'A' : c - 'a') + 10;

        private string? ReadHyperlinkUrl(string group)
        {
            const string marker = "HYPERLINK \"";
            int start = group.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = group.IndexOf('"', start);
            return end < 0 ? null : DecodeRtfText(group.Substring(start, end - start));
        }

        private string? ReadFieldResult(string group)
        {
            const string marker = @"{\fldrslt ";
            int start = group.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            int end = FindLocalGroupEnd(group, start - marker.Length);
            return end <= start ? null : DecodeRtfText(group.Substring(start, end - start));
        }

        private static int FindLocalGroupEnd(string value, int groupStart)
        {
            int depth = 0;
            for (int i = groupStart; i < value.Length; i++)
            {
                if (value[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (value[i] == '{')
                {
                    depth++;
                }
                else if (value[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return value.Length - 1;
        }

        private void ReadColorTable(int groupStart)
        {
            int groupEnd = FindGroupEnd(groupStart);
            int red = -1;
            int green = -1;
            int blue = -1;
            for (int i = groupStart; i <= groupEnd;)
            {
                if (_rtf[i] == '\\')
                {
                    if (TryReadColorComponent(i, "red", out int component, out int next))
                    {
                        red = component;
                        i = next;
                        continue;
                    }

                    if (TryReadColorComponent(i, "green", out component, out next))
                    {
                        green = component;
                        i = next;
                        continue;
                    }

                    if (TryReadColorComponent(i, "blue", out component, out next))
                    {
                        blue = component;
                        i = next;
                        continue;
                    }
                }
                else if (_rtf[i] == ';')
                {
                    if (red >= 0 && green >= 0 && blue >= 0)
                    {
                        _colors.Add("#" + red.ToString("X2", CultureInfo.InvariantCulture) +
                            green.ToString("X2", CultureInfo.InvariantCulture) +
                            blue.ToString("X2", CultureInfo.InvariantCulture));
                    }

                    red = green = blue = -1;
                }

                i++;
            }
        }

        private bool TryReadColorComponent(int index, string name, out int value, out int next)
        {
            value = -1;
            next = index;
            string marker = "\\" + name;
            if (!StartsWith(index, marker))
            {
                return false;
            }

            int pos = index + marker.Length;
            int start = pos;
            while (pos < _rtf.Length && char.IsDigit(_rtf[pos]))
            {
                pos++;
            }

            next = pos;
            return pos > start &&
                int.TryParse(_rtf.Substring(start, pos - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private string DecodeGroupText(int groupStart, bool skipFirstControl)
        {
            int groupEnd = FindGroupEnd(groupStart);
            int start = groupStart + 1;
            if (skipFirstControl && start < groupEnd && _rtf[start] == '\\')
            {
                start = ReadSkippedControl(start);
            }

            return DecodeRtfText(_rtf.Substring(start, Math.Max(0, groupEnd - start)));
        }

        private string DecodeDestinationGroupText(int groupStart, string controlWord)
        {
            int groupEnd = FindGroupEnd(groupStart);
            int start = groupStart + 1;
            if (start < groupEnd && StartsWith(start, @"\*"))
            {
                start += 2;
                if (start < groupEnd && _rtf[start] == ' ')
                {
                    start++;
                }
            }

            if (start < groupEnd && StartsWith(start, "\\" + controlWord))
            {
                start = ReadSkippedControl(start);
            }

            return DecodeRtfText(_rtf.Substring(start, Math.Max(0, groupEnd - start)));
        }

        private string DecodeRtfText(string value)
        {
            var sb = new StringBuilder(value.Length);
            int unicodeFallbackLength = 1;
            for (int i = 0; i < value.Length;)
            {
                char c = value[i];
                if (c == '\\')
                {
                    if (i + 1 >= value.Length)
                    {
                        i++;
                    }
                    else if (value[i + 1] is '\\' or '{' or '}')
                    {
                        sb.Append(value[i + 1]);
                        i += 2;
                    }
                    else if (value[i + 1] == '\'' && TryDecodeAnsiHexEscape(value, i, out char decoded))
                    {
                        sb.Append(decoded);
                        i += 4;
                    }
                    else if (StartsWith(value, i, "\\line"))
                    {
                        sb.Append('\n');
                        i = SkipControl(value, i);
                    }
                    else if (StartsWith(value, i, "\\tab"))
                    {
                        sb.Append('\t');
                        i = SkipControl(value, i);
                    }
                    else if (TryAppendSimpleTextControl(value, i, sb, out int nextIndex))
                    {
                        i = nextIndex;
                    }
                    else if (StartsWith(value, i, "\\uc"))
                    {
                        i = ReadUnicodeFallbackLength(value, i, out unicodeFallbackLength);
                    }
                    else if (StartsWith(value, i, "\\u"))
                    {
                        i = ReadUnicodeEscape(value, i, sb, unicodeFallbackLength);
                    }
                    else
                    {
                        i = SkipControl(value, i);
                    }
                }
                else if (c is '{' or '}')
                {
                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static bool TryAppendSimpleTextControl(string value, int index, StringBuilder sb, out int nextIndex)
        {
            nextIndex = index;
            foreach ((string Control, char Character) mapping in SimpleTextControls)
            {
                if (StartsWith(value, index, mapping.Control))
                {
                    sb.Append(mapping.Character);
                    nextIndex = SkipControl(value, index);
                    return true;
                }
            }

            return false;
        }

        private static readonly (string Control, char Character)[] SimpleTextControls =
        [
            ("\\bullet", '\u2022'),
            ("\\enspace", '\u2002'),
            ("\\emspace", '\u2003'),
            ("\\qmspace", '\u2005'),
            ("\\emdash", '\u2014'),
            ("\\endash", '\u2013'),
            ("\\lquote", '\u2018'),
            ("\\rquote", '\u2019'),
            ("\\ldblquote", '\u201C'),
            ("\\rdblquote", '\u201D'),
        ];

        private static int ReadUnicodeEscape(string value, int index, StringBuilder sb, int fallbackLength)
        {
            int pos = index + 2;
            bool negative = pos < value.Length && value[pos] == '-';
            if (negative)
            {
                pos++;
            }

            int start = pos;
            while (pos < value.Length && char.IsDigit(value[pos]))
            {
                pos++;
            }

            if (pos > start &&
                int.TryParse(value.Substring(start, pos - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                sb.Append(DecodeSignedUnicodeEscape(negative ? -parsed : parsed));
            }

            if (pos < value.Length && value[pos] == ' ')
            {
                pos++;
            }

            return SkipUnicodeFallback(value, pos, fallbackLength);
        }

        private static int ReadUnicodeFallbackLength(string value, int index, out int fallbackLength)
        {
            int pos = index + 3;
            bool negative = pos < value.Length && value[pos] == '-';
            if (negative)
            {
                pos++;
            }

            int start = pos;
            while (pos < value.Length && char.IsDigit(value[pos]))
            {
                pos++;
            }

            fallbackLength = 1;
            if (!negative &&
                pos > start &&
                int.TryParse(value.Substring(start, pos - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                fallbackLength = parsed;
            }

            if (pos < value.Length && value[pos] == ' ')
            {
                pos++;
            }

            return pos;
        }

        private static char DecodeSignedUnicodeEscape(int value)
        {
            return unchecked((char)(short)value);
        }

        private static int SkipUnicodeFallback(string value, int index, int count)
        {
            int pos = index;
            for (int i = 0; i < count && pos < value.Length; i++)
            {
                if (value[pos] == '\\')
                {
                    if (pos + 1 < value.Length && value[pos + 1] == '\'' && TryDecodeAnsiHexEscape(value, pos, out _))
                    {
                        pos += 4;
                    }
                    else if (pos + 1 < value.Length && value[pos + 1] is '\\' or '{' or '}')
                    {
                        pos += 2;
                    }
                    else
                    {
                        pos = SkipControl(value, pos);
                    }
                }
                else
                {
                    pos++;
                }
            }

            return pos;
        }

        private static bool TryDecodeAnsiHexEscape(string value, int index, out char decoded)
        {
            decoded = '\0';
            if (index + 3 >= value.Length ||
                value[index] != '\\' ||
                value[index + 1] != '\'' ||
                !TryReadHexNibble(value[index + 2], out int high) ||
                !TryReadHexNibble(value[index + 3], out int low))
            {
                return false;
            }

            int code = (high << 4) | low;
            decoded = DecodeWindows1252Byte(code);
            return true;
        }

        private static bool TryReadHexNibble(char c, out int value)
        {
            if (c is >= '0' and <= '9')
            {
                value = c - '0';
                return true;
            }

            if (c is >= 'a' and <= 'f')
            {
                value = c - 'a' + 10;
                return true;
            }

            if (c is >= 'A' and <= 'F')
            {
                value = c - 'A' + 10;
                return true;
            }

            value = 0;
            return false;
        }

        private static char DecodeWindows1252Byte(int value)
        {
            return value switch
            {
                0x80 => '\u20AC',
                0x82 => '\u201A',
                0x83 => '\u0192',
                0x84 => '\u201E',
                0x85 => '\u2026',
                0x86 => '\u2020',
                0x87 => '\u2021',
                0x88 => '\u02C6',
                0x89 => '\u2030',
                0x8A => '\u0160',
                0x8B => '\u2039',
                0x8C => '\u0152',
                0x8E => '\u017D',
                0x91 => '\u2018',
                0x92 => '\u2019',
                0x93 => '\u201C',
                0x94 => '\u201D',
                0x95 => '\u2022',
                0x96 => '\u2013',
                0x97 => '\u2014',
                0x98 => '\u02DC',
                0x99 => '\u2122',
                0x9A => '\u0161',
                0x9B => '\u203A',
                0x9C => '\u0153',
                0x9E => '\u017E',
                0x9F => '\u0178',
                _ => (char)value,
            };
        }

        private static bool StartsWith(string text, int index, string value)
        {
            if (index + value.Length > text.Length)
            {
                return false;
            }

            return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }

        private int ReadSkippedControl(int index)
        {
            int pos = index + 1;
            while (pos < _rtf.Length && char.IsLetter(_rtf[pos]))
            {
                pos++;
            }

            while (pos < _rtf.Length && (char.IsDigit(_rtf[pos]) || _rtf[pos] == '-'))
            {
                pos++;
            }

            if (pos < _rtf.Length && _rtf[pos] == ' ')
            {
                pos++;
            }

            return pos;
        }

        private static int SkipControl(string value, int index)
        {
            int pos = index + 1;
            while (pos < value.Length && char.IsLetter(value[pos]))
            {
                pos++;
            }

            while (pos < value.Length && (char.IsDigit(value[pos]) || value[pos] == '-'))
            {
                pos++;
            }

            if (pos < value.Length && value[pos] == ' ')
            {
                pos++;
            }

            return pos;
        }

        private bool StartsGroup(int index, string controlWord) =>
            StartsWith(index, "{\\" + controlWord);

        private bool StartsDestinationGroup(int index, string controlWord) =>
            StartsWith(index, "{\\" + controlWord) ||
            StartsWith(index, "{\\*\\" + controlWord);

        private bool StartsWith(int index, string value)
        {
            if (index + value.Length > _rtf.Length)
            {
                return false;
            }

            return string.CompareOrdinal(_rtf, index, value, 0, value.Length) == 0;
        }

        private int FindGroupEnd(int groupStart)
        {
            int depth = 0;
            for (int i = groupStart; i < _rtf.Length; i++)
            {
                if (_rtf[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (_rtf[i] == '{')
                {
                    depth++;
                }
                else if (_rtf[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return _rtf.Length - 1;
        }

        private static string FormatHalfPoints(int halfPoints) =>
            Math.Max(1, halfPoints / 2d).ToString("0.##", CultureInfo.InvariantCulture) + "pt";

        private string? ReadColor(int? index)
        {
            if (_colors.Count == 0)
            {
                int colorTableStart = _rtf.IndexOf(@"{\colortbl", StringComparison.Ordinal);
                if (colorTableStart >= 0)
                {
                    ReadColorTable(colorTableStart);
                }
            }

            if (!index.HasValue || index.Value <= 0 || index.Value > _colors.Count)
            {
                return null;
            }

            return _colors[index.Value - 1];
        }
    }

    private readonly struct CellInfo
    {
        public CellInfo(string text, int rightBoundary)
        {
            Text = text;
            RightBoundary = rightBoundary;
        }

        public string Text { get; }
        public int RightBoundary { get; }
    }

    private readonly record struct InlineStyle(
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        bool Strikethrough = false,
        string? TextPosition = null,
        string? FontSize = null,
        string? Color = null)
    {
        public static InlineStyle Empty { get; } = new();
    }
}
