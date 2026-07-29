using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Describes one bounded cell replacement for <see cref="OdsSparseEditor"/>.
/// 描述 <see cref="OdsSparseEditor"/> 的單一有界儲存格取代項目。
/// </summary>
public sealed class OdsCellPatch
{
    /// <summary>
    /// Gets or sets the worksheet name.
    /// 取得或設定工作表名稱。
    /// </summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based row index.
    /// 取得或設定以零為起點的列索引。
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Gets or sets the zero-based column index.
    /// 取得或設定以零為起點的欄索引。
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Gets or sets the replacement text, or null to preserve the current value.
    /// 取得或設定取代文字；null 表示保留目前值。
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the replacement ODF formula, or null to preserve the current formula.
    /// 取得或設定取代用 ODF 公式；null 表示保留目前公式。
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Gets or sets an existing cell style name, or null to preserve the current style.
    /// 取得或設定既有儲存格樣式名稱；null 表示保留目前樣式。
    /// </summary>
    public string? StyleName { get; set; }

    /// <summary>
    /// Gets or sets the number of rows merged from the target cell.
    /// 取得或設定自目標儲存格起合併的列數。
    /// </summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of columns merged from the target cell.
    /// 取得或設定自目標儲存格起合併的欄數。
    /// </summary>
    public int ColumnSpan { get; set; } = 1;
}

/// <summary>
/// Configures bounded streaming edits of an existing ODS package.
/// 設定既有 ODS 封裝的有界串流編輯。
/// </summary>
public sealed class OdsSparseEditorOptions
{
    /// <summary>
    /// Gets or sets package input limits.
    /// 取得或設定封裝輸入限制。
    /// </summary>
    public OdfLoadOptions LoadOptions { get; set; } = OdfLoadOptions.Default;

    /// <summary>
    /// Gets or sets the maximum patch count.
    /// 取得或設定修補項目數上限。
    /// </summary>
    public int MaximumPatches { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum characters accepted in one replacement.
    /// 取得或設定單一取代值允許的最大字元數。
    /// </summary>
    public int MaximumReplacementCharacters { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum characters accepted in one formula or style name.
    /// 取得或設定單一公式或樣式名稱允許的最大字元數。
    /// </summary>
    public int MaximumMetadataCharacters { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the maximum tokens accepted in one formula.
    /// 取得或設定單一公式允許的最大語彙基元數。
    /// </summary>
    public int MaximumFormulaTokens { get; set; } = 512;

    /// <summary>
    /// Gets or sets the maximum parenthesis or array nesting depth in one formula.
    /// 取得或設定單一公式的最大括號或陣列巢狀深度。
    /// </summary>
    public int MaximumFormulaDepth { get; set; } = 64;

    /// <summary>
    /// Gets or sets the maximum cells covered by all requested merges.
    /// 取得或設定所有合併要求可涵蓋的儲存格總數上限。
    /// </summary>
    public int MaximumMergedCells { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum style declarations indexed for reference validation.
    /// 取得或設定為參照驗證建立索引的樣式宣告數上限。
    /// </summary>
    public int MaximumStyleDeclarations { get; set; } = 250_000;

    /// <summary>
    /// Gets or sets the maximum logical repeated rows or columns expanded around a patch.
    /// 取得或設定單一修補可拆分的邏輯重複列或欄上限。
    /// </summary>
    public int MaximumRepeat { get; set; } = 10_000_000;
}

/// <summary>
/// Applies coordinate-based edits to an existing ODS package without materializing the sheet DOM.
/// 在不具現化工作表 DOM 的情況下，對既有 ODS 封裝套用座標式編輯。
/// </summary>
public static class OdsSparseEditor
{
    private static readonly XNamespace Table = OdfNamespaces.Table;
    private static readonly XNamespace Office = OdfNamespaces.Office;
    private static readonly XNamespace Text = OdfNamespaces.Text;
    private static readonly XNamespace Style = OdfNamespaces.Style;

    private sealed class CellEdit(OdsCellPatch patch, int row, int column, bool covered)
    {
        internal OdsCellPatch Patch { get; } = patch;

        internal int Row { get; } = row;

        internal int Column { get; } = column;

        internal bool Covered { get; } = covered;
    }

    /// <summary>
    /// Applies cell patches to a file through a same-directory temporary file and atomic replacement.
    /// 透過同目錄暫存檔與原子取代，對檔案套用儲存格修補。
    /// </summary>
    /// <param name="sourcePath">The source ODS path. / 來源 ODS 路徑。</param>
    /// <param name="destinationPath">The destination ODS path, which may equal the source path. / 目的 ODS 路徑，可與來源相同。</param>
    /// <param name="patches">The cell patches. / 儲存格修補項目。</param>
    /// <param name="options">The resource limits. / 資源限制。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the operation. / 代表作業的工作。</returns>
    public static async Task ApplyFileAsync(
        string sourcePath,
        string destinationPath,
        IEnumerable<OdsCellPatch> patches,
        OdsSparseEditorOptions? options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(sourcePath, nameof(sourcePath));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destinationPath, nameof(destinationPath));
        string sourceFullPath = Path.GetFullPath(sourcePath);
        string destinationFullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(destinationFullPath) ?? Directory.GetCurrentDirectory();
        string temporaryPath = Path.Combine(
            directory,
            "." + Path.GetFileName(destinationFullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool completed = false;
        try
        {
            using (var source = new FileStream(
                sourceFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await ApplyAsync(
                    source,
                    destination,
                    patches,
                    options,
                    cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (File.Exists(destinationFullPath))
                File.Replace(temporaryPath, destinationFullPath, null);
            else
                File.Move(temporaryPath, destinationFullPath);
            completed = true;
        }
        finally
        {
            if (!completed && File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Applies cell patches while streaming the source package to the destination.
    /// 將來源封裝串流至目的地，同時套用儲存格修補。
    /// </summary>
    /// <param name="source">The seekable source ODS stream. / 可搜尋的來源 ODS 串流。</param>
    /// <param name="destination">The empty writable destination stream. / 空白且可寫入的目的串流。</param>
    /// <param name="patches">The cell patches. / 儲存格修補項目。</param>
    /// <param name="options">The resource limits. / 資源限制。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the operation. / 代表作業的工作。</returns>
    public static async Task ApplyAsync(
        Stream source,
        Stream destination,
        IEnumerable<OdsCellPatch> patches,
        OdsSparseEditorOptions? options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(source, nameof(source));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(patches, nameof(patches));
        options ??= new OdsSparseEditorOptions();
        ValidateOptions(options);
        if (!source.CanRead || !source.CanSeek)
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"),
                nameof(source));
        if (!destination.CanWrite)
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"),
                nameof(destination));
        if (ReferenceEquals(source, destination))
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"),
                nameof(destination));

        Dictionary<(string Sheet, int Row, int Column), CellEdit> patchMap =
            MaterializePatches(patches, options, cancellationToken);
        using var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        ValidateArchive(input, options.LoadOptions);
        if (HasEncryptedEntries(input, options.LoadOptions))
            throw new NotSupportedException();
        HashSet<string> styleNames = CollectStyleNames(
            input.GetEntry("styles.xml"),
            options,
            cancellationToken);
        long totalSize = 0;
        bool contentSeen = false;
        var entryNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in input.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = OdfPackage.SanitizeEntryName(entry.FullName);
            if (!entryNames.Add(name))
                throw new InvalidDataException();
            ValidateEntry(entry, name, options.LoadOptions, ref totalSize);
            CompressionLevel compression = string.Equals(name, "mimetype", StringComparison.Ordinal)
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
            ZipArchiveEntry target = output.CreateEntry(name, compression);
            using Stream inputStream = entry.Open();
            using Stream outputStream = target.Open();
            if (string.Equals(name, "content.xml", StringComparison.Ordinal))
            {
                contentSeen = true;
                TransformContent(
                    inputStream,
                    outputStream,
                    patchMap,
                    styleNames,
                    options,
                    cancellationToken);
            }
            else
            {
                await OdfBoundedStreamReader.CopyToAsync(
                    inputStream,
                    outputStream,
                    options.LoadOptions.MaxEntrySize,
                    "Err_OdfPackage_InputStreamSizeLimitExceeded",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        if (!contentSeen || patchMap.Count != 0)
            throw new InvalidDataException();
    }

    private static Dictionary<(string Sheet, int Row, int Column), CellEdit> MaterializePatches(
        IEnumerable<OdsCellPatch> patches,
        OdsSparseEditorOptions options,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, int, int), CellEdit>();
        int patchCount = 0;
        long mergedCells = 0;
        foreach (OdsCellPatch patch in patches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(patch, nameof(patches));
            if (patchCount++ >= options.MaximumPatches ||
                string.IsNullOrWhiteSpace(patch.SheetName) ||
                patch.Row < 0 ||
                patch.Column < 0 ||
                patch.RowSpan < 1 ||
                patch.ColumnSpan < 1 ||
                patch.RowSpan > options.MaximumRepeat ||
                patch.ColumnSpan > options.MaximumRepeat ||
                (patch.Text?.Length ?? 0) > options.MaximumReplacementCharacters ||
                (patch.Formula?.Length ?? 0) > options.MaximumMetadataCharacters ||
                (patch.StyleName?.Length ?? 0) > options.MaximumMetadataCharacters ||
                (patch.Text is not null && patch.Formula is not null))
            {
                throw new ArgumentOutOfRangeException(nameof(patches));
            }
            ValidateFormula(patch.Formula, options);
            long patchCells = checked((long)patch.RowSpan * patch.ColumnSpan);
            mergedCells = checked(mergedCells + patchCells);
            if (mergedCells > options.MaximumMergedCells)
                throw new ArgumentOutOfRangeException(nameof(patches));
            for (int rowOffset = 0; rowOffset < patch.RowSpan; rowOffset++)
            {
                int row = checked(patch.Row + rowOffset);
                for (int columnOffset = 0; columnOffset < patch.ColumnSpan; columnOffset++)
                {
                    int column = checked(patch.Column + columnOffset);
                    var key = (patch.SheetName, row, column);
                    if (result.ContainsKey(key))
                    {
                        throw new ArgumentException(
                            OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellAddress", column),
                            nameof(patches));
                    }
                    result.Add(key, new CellEdit(
                        patch,
                        row,
                        column,
                        rowOffset != 0 || columnOffset != 0));
                }
            }
        }
        return result;
    }

    private static void ValidateFormula(
        string? formula,
        OdsSparseEditorOptions options)
    {
        if (formula is null)
            return;
        if (!(formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase) ||
            formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_FormulaParser_UnexpectedTokenEndFormula", formula),
                nameof(formula));
        }
        string normalized = FormulaPrefixNormalizer.RemovePrefix(
            OdfFormulaTranslator.OdfToExcelFormula(formula));
        var tokenizer = new Tokenizer(normalized.AsSpan());
        int tokenCount = 0;
        int depth = 0;
        while (true)
        {
            FormulaParserToken token = tokenizer.NextToken();
            if (token.Type == FormulaTokenType.EndOfFormula)
                break;
            if (++tokenCount > options.MaximumFormulaTokens)
                throw new InvalidDataException();
            if (token.Type is FormulaTokenType.OpenParen or FormulaTokenType.OpenBrace)
            {
                if (++depth > options.MaximumFormulaDepth)
                    throw new InvalidDataException();
            }
            else if (token.Type is FormulaTokenType.CloseParen or FormulaTokenType.CloseBrace)
            {
                depth--;
                if (depth < 0)
                    throw new InvalidDataException();
            }
        }
        if (depth != 0)
            throw new InvalidDataException();
        _ = new FormulaParser(normalized).Parse();
    }

    private static void TransformContent(
        Stream input,
        Stream output,
        Dictionary<(string Sheet, int Row, int Column), CellEdit> patches,
        HashSet<string> styleNames,
        OdsSparseEditorOptions options,
        CancellationToken cancellationToken)
    {
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = options.LoadOptions.MaxXmlCharactersInDocument,
            IgnoreWhitespace = false,
            CloseInput = false,
        };
        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            CloseOutput = false,
            Indent = false,
        };
        using XmlReader reader = XmlReader.Create(input, readerSettings);
        using XmlWriter writer = XmlWriter.Create(output, writerSettings);
        Dictionary<string, Queue<KeyValuePair<int, List<CellEdit>>>> patchRows = patches
            .Values
            .GroupBy(edit => edit.Patch.SheetName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<KeyValuePair<int, List<CellEdit>>>(
                    group.GroupBy(edit => edit.Row)
                        .OrderBy(rowGroup => rowGroup.Key)
                        .Select(rowGroup => new KeyValuePair<int, List<CellEdit>>(
                            rowGroup.Key,
                            rowGroup.OrderBy(edit => edit.Column).ToList()))),
                StringComparer.Ordinal);
        string? currentSheet = null;
        int logicalRow = 0;
        bool processCurrent = false;
        while (processCurrent || reader.Read())
        {
            processCurrent = false;
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element)
                IndexStyleDeclaration(reader, styleNames, options);
            if (reader.NodeType == XmlNodeType.Element &&
                reader.NamespaceURI == OdfNamespaces.Table &&
                reader.LocalName == "table")
            {
                currentSheet = reader.GetAttribute("name", OdfNamespaces.Table);
                logicalRow = 0;
                WriteStartElement(reader, writer);
                continue;
            }
            if (reader.NodeType == XmlNodeType.Element &&
                reader.NamespaceURI == OdfNamespaces.Table &&
                reader.LocalName == "table-row" &&
                currentSheet is not null)
            {
                XElement row = (XElement)XNode.ReadFrom(reader);
                int repeat = GetRepeat(row, Table + "number-rows-repeated", options.MaximumRepeat);
                int rowEnd = checked(logicalRow + repeat);
                var rowPatches = new List<CellEdit>();
                if (patchRows.TryGetValue(currentSheet, out Queue<KeyValuePair<int, List<CellEdit>>>? queue))
                {
                    if (queue.Count > 0 && queue.Peek().Key < logicalRow)
                        throw new InvalidDataException();
                    while (queue.Count > 0 && queue.Peek().Key < rowEnd)
                        rowPatches.AddRange(queue.Dequeue().Value);
                }
                WritePatchedRows(
                    writer,
                    row,
                    logicalRow,
                    repeat,
                    rowPatches,
                    patches,
                    styleNames,
                    options);
                logicalRow = rowEnd;
                processCurrent = reader.ReadState == ReadState.Interactive;
                continue;
            }
            WriteNode(reader, writer);
        }
    }

    private static void WritePatchedRows(
        XmlWriter writer,
        XElement source,
        int rowStart,
        int repeat,
        List<CellEdit> rowPatches,
        Dictionary<(string Sheet, int Row, int Column), CellEdit> remaining,
        HashSet<string> styleNames,
        OdsSparseEditorOptions options)
    {
        int cursor = rowStart;
        foreach (IGrouping<int, CellEdit> group in rowPatches.GroupBy(edit => edit.Row))
        {
            if (group.Key > cursor)
                WriteRowClone(writer, source, group.Key - cursor);
            XElement target = new(source);
            target.Attribute(Table + "number-rows-repeated")?.Remove();
            foreach (CellEdit edit in group)
            {
                PatchCell(target, edit.Column, edit, styleNames, options);
                remaining.Remove((edit.Patch.SheetName, edit.Row, edit.Column));
            }
            target.WriteTo(writer);
            cursor = group.Key + 1;
        }
        int end = checked(rowStart + repeat);
        if (cursor < end)
            WriteRowClone(writer, source, end - cursor);
    }

    private static void PatchCell(
        XElement row,
        int targetColumn,
        CellEdit edit,
        HashSet<string> styleNames,
        OdsSparseEditorOptions options)
    {
        int column = 0;
        foreach (XElement cell in row.Elements()
            .Where(element => element.Name == Table + "table-cell" ||
                element.Name == Table + "covered-table-cell")
            .ToList())
        {
            int repeat = GetRepeat(cell, Table + "number-columns-repeated", options.MaximumRepeat);
            int end = checked(column + repeat);
            if (targetColumn < end)
            {
                if (cell.Name != Table + "table-cell" ||
                    cell.Attribute(Table + "number-columns-spanned") is not null ||
                    cell.Attribute(Table + "number-rows-spanned") is not null)
                    throw new InvalidDataException();
                int before = targetColumn - column;
                int after = repeat - before - 1;
                if (before > 0)
                    cell.AddBeforeSelf(CloneWithRepeat(cell, Table + "number-columns-repeated", before));
                XElement replacement = CreateReplacement(cell, edit, styleNames);
                cell.AddBeforeSelf(replacement);
                if (after > 0)
                    cell.AddBeforeSelf(CloneWithRepeat(cell, Table + "number-columns-repeated", after));
                cell.Remove();
                return;
            }
            column = end;
        }
        throw new InvalidDataException();
    }

    private static XElement CreateReplacement(
        XElement source,
        CellEdit edit,
        HashSet<string> styleNames)
    {
        OdsCellPatch patch = edit.Patch;
        if (edit.Covered)
        {
            if (!IsBlankCell(source))
                throw new InvalidDataException();
            var covered = new XElement(Table + "covered-table-cell");
            XAttribute? style = source.Attribute(Table + "style-name");
            if (style is not null)
                covered.SetAttributeValue(Table + "style-name", style.Value);
            return covered;
        }

        XElement replacement = new(source);
        replacement.Attribute(Table + "number-columns-repeated")?.Remove();
        if (patch.StyleName is not null)
        {
            if (!styleNames.Contains(patch.StyleName))
                throw new InvalidDataException();
            replacement.SetAttributeValue(Table + "style-name", patch.StyleName);
        }
        if (patch.Text is not null)
        {
            ClearCellValue(replacement);
            replacement.SetAttributeValue(Office + "value-type", "string");
            replacement.Add(new XElement(Text + "p", patch.Text));
        }
        else if (patch.Formula is not null)
        {
            ClearCellValue(replacement);
            replacement.SetAttributeValue(Table + "formula", patch.Formula);
        }
        if (patch.ColumnSpan > 1)
            replacement.SetAttributeValue(Table + "number-columns-spanned", patch.ColumnSpan);
        if (patch.RowSpan > 1)
            replacement.SetAttributeValue(Table + "number-rows-spanned", patch.RowSpan);
        return replacement;
    }

    private static void ClearCellValue(XElement cell)
    {
        cell.Attribute(Office + "value-type")?.Remove();
        cell.Attribute(Office + "value")?.Remove();
        cell.Attribute(Office + "string-value")?.Remove();
        cell.Attribute(Office + "boolean-value")?.Remove();
        cell.Attribute(Office + "currency")?.Remove();
        cell.Attribute(Office + "date-value")?.Remove();
        cell.Attribute(Office + "time-value")?.Remove();
        cell.Attribute(Table + "formula")?.Remove();
        cell.Elements(Text + "p").Remove();
    }

    private static bool IsBlankCell(XElement cell)
    {
        if (cell.Attributes().Any(attribute =>
            attribute.Name != Table + "number-columns-repeated" &&
            attribute.Name != Table + "style-name"))
        {
            return false;
        }
        return !cell.Nodes().Any(node =>
            node is XElement element
                ? element.Name != Text + "p" || !string.IsNullOrEmpty(element.Value)
                : node is XText text && !string.IsNullOrWhiteSpace(text.Value));
    }

    private static HashSet<string> CollectStyleNames(
        ZipArchiveEntry? entry,
        OdsSparseEditorOptions options,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (entry is null)
            return result;
        if (entry.Length > options.LoadOptions.MaxEntrySize)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage(
                    "Err_OdfPackage_ZipEntrySizeLimitExceeded",
                    entry.FullName,
                    entry.Length,
                    options.LoadOptions.MaxEntrySize));
        }
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = options.LoadOptions.MaxXmlCharactersInDocument,
        };
        using Stream stream = entry.Open();
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element)
                IndexStyleDeclaration(reader, result, options);
        }
        return result;
    }

    private static void IndexStyleDeclaration(
        XmlReader reader,
        HashSet<string> styleNames,
        OdsSparseEditorOptions options)
    {
        if (reader.NamespaceURI != OdfNamespaces.Style ||
            reader.LocalName != "style" ||
            !string.Equals(
                reader.GetAttribute("family", OdfNamespaces.Style),
                "table-cell",
                StringComparison.Ordinal))
        {
            return;
        }
        string? name = reader.GetAttribute("name", OdfNamespaces.Style);
        if (string.IsNullOrEmpty(name) ||
            name.Length > options.MaximumMetadataCharacters ||
            (!styleNames.Contains(name) && styleNames.Count >= options.MaximumStyleDeclarations))
        {
            throw new InvalidDataException();
        }
        styleNames.Add(name);
    }

    private static void WriteRowClone(XmlWriter writer, XElement source, int repeat)
    {
        XElement clone = CloneWithRepeat(source, Table + "number-rows-repeated", repeat);
        clone.WriteTo(writer);
    }

    private static XElement CloneWithRepeat(XElement source, XName attribute, int repeat)
    {
        var clone = new XElement(source);
        if (repeat == 1)
            clone.Attribute(attribute)?.Remove();
        else
            clone.SetAttributeValue(attribute, repeat);
        return clone;
    }

    private static int GetRepeat(XElement element, XName attribute, int maximum)
    {
        string? value = (string?)element.Attribute(attribute);
        if (value is null)
            return 1;
        if (!int.TryParse(value, out int repeat) || repeat < 1 || repeat > maximum)
            throw new InvalidDataException();
        return repeat;
    }

    private static void WriteStartElement(XmlReader reader, XmlWriter writer)
    {
        writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
        if (reader.HasAttributes)
        {
            while (reader.MoveToNextAttribute())
                writer.WriteAttributeString(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value);
            reader.MoveToElement();
        }
        if (reader.IsEmptyElement)
            writer.WriteEndElement();
    }

    private static void WriteNode(XmlReader reader, XmlWriter writer)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                WriteStartElement(reader, writer);
                break;
            case XmlNodeType.EndElement:
                writer.WriteFullEndElement();
                break;
            case XmlNodeType.Text:
                writer.WriteString(reader.Value);
                break;
            case XmlNodeType.CDATA:
                writer.WriteCData(reader.Value);
                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                writer.WriteWhitespace(reader.Value);
                break;
            case XmlNodeType.XmlDeclaration:
                writer.WriteProcessingInstruction("xml", reader.Value);
                break;
            case XmlNodeType.ProcessingInstruction:
                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                break;
            case XmlNodeType.Comment:
                writer.WriteComment(reader.Value);
                break;
        }
    }

    private static void ValidateOptions(OdsSparseEditorOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options.LoadOptions, nameof(options));
        if (options.MaximumPatches < 1 ||
            options.MaximumReplacementCharacters < 0 ||
            options.MaximumMetadataCharacters < 1 ||
            options.MaximumFormulaTokens < 1 ||
            options.MaximumFormulaDepth < 1 ||
            options.MaximumMergedCells < 1 ||
            options.MaximumStyleDeclarations < 1 ||
            options.MaximumRepeat < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static void ValidateArchive(ZipArchive archive, OdfLoadOptions options)
    {
        if (archive.Entries.Count > options.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage(
                    "Err_OdfPackage_ZipEntryCountLimitExceeded",
                    archive.Entries.Count,
                    options.MaxZipEntries));
        }
        if (archive.Entries.Count == 0 ||
            !string.Equals(archive.Entries[0].FullName, "mimetype", StringComparison.Ordinal) ||
            archive.Entries[0].Length > 256)
        {
            throw new InvalidDataException();
        }
        using (var mimeEntry = new OdfPackageEntry("mimetype", archive.Entries[0]))
        {
            if (!mimeEntry.WasStoredInZip)
                throw new InvalidDataException();
        }
        using Stream mimeStream = archive.Entries[0].Open();
        using var mimeReader = new StreamReader(
            mimeStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 256,
            leaveOpen: false);
        string mimeType = mimeReader.ReadToEnd();
        if (!string.Equals(
                mimeType,
                "application/vnd.oasis.opendocument.spreadsheet",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException();
        }
    }

    private static bool HasEncryptedEntries(ZipArchive archive, OdfLoadOptions options)
    {
        ZipArchiveEntry? manifest = archive.GetEntry("META-INF/manifest.xml");
        if (manifest is null)
            return false;
        if (manifest.Length > options.MaxEntrySize)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage(
                    "Err_OdfPackage_ZipEntrySizeLimitExceeded",
                    manifest.FullName,
                    manifest.Length,
                    options.MaxEntrySize));
        }
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = options.MaxXmlCharactersInDocument,
        };
        using Stream stream = manifest.Open();
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "encryption-data" &&
                reader.NamespaceURI == OdfNamespaces.Manifest)
            {
                return true;
            }
        }
        return false;
    }

    private static void ValidateEntry(
        ZipArchiveEntry entry,
        string name,
        OdfLoadOptions options,
        ref long totalSize)
    {
        if (entry.Length > options.MaxEntrySize)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage(
                    "Err_OdfPackage_ZipEntrySizeLimitExceeded",
                    name,
                    entry.Length,
                    options.MaxEntrySize));
        }
        totalSize = OdfBoundedStreamReader.AddBytes(
            totalSize,
            entry.Length,
            options.MaxTotalUncompressedSize,
            "Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded");
    }
}
