using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

// Native AOT / trimming 煙霧測試：觸及主要公開 API 根，供 IL 連結器驗證（PERF-5e）。
int checks = 0;
checks += SmokeSpreadsheetRoundTrip();
checks += SmokeChartWithDefinition();
checks += SmokeTextPresentationDrawing();
checks += SmokeFormulaDocument();
checks += SmokeDocumentFactoryKinds();
checks += SmokeFormulaEvaluation();
checks += SmokeXmlWriterRoundTrip();
checks += SmokeEmbeddedDocumentFactory();

Console.WriteLine($"TrimSmoke OK: {checks} API 根通過");

static int SmokeSpreadsheetRoundTrip()
{
    using var stream = new MemoryStream();
    using var document = SpreadsheetDocument.Create();
    document.AddSheet("Sheet1");
    document.SaveToStream(stream);

    stream.Position = 0;
    using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
    using Stream contentStream = package.GetEntryStream("content.xml");
    OdfNode root = OdfXmlReader.Parse(contentStream);

    if (root.LocalName.Length == 0 || document.GetSheets().Count != 1)
    {
        throw new InvalidOperationException("試算表往返失敗。");
    }

    return 1;
}

static int SmokeChartWithDefinition()
{
    var definition = new OdfChartDefinition
    {
        ChartType = OdfChartType.Bar,
        Title = "TrimSmoke",
        DataRange = new OdfCellRange(0, 0, 2, 1),
    };

    using var chartDoc = ChartDocument.Create(definition);
    using var stream = new MemoryStream();
    chartDoc.SaveToStream(stream);

    if (stream.Length == 0 || chartDoc.GetChartDefinition().Title != "TrimSmoke")
    {
        throw new InvalidOperationException("圖表建立失敗。");
    }

    return 1;
}

static int SmokeTextPresentationDrawing()
{
    using var textDoc = TextDocument.Create();
    textDoc.AddParagraph("TrimSmoke");
    using var textStream = new MemoryStream();
    textDoc.SaveToStream(textStream);

    using var presDoc = PresentationDocument.Create();
    presDoc.AddSlide("Slide1");
    using var presStream = new MemoryStream();
    presDoc.SaveToStream(presStream);

    using var drawDoc = DrawingDocument.Create();
    drawDoc.AddPage("Page1");
    using var drawStream = new MemoryStream();
    drawDoc.SaveToStream(drawStream);

    if (textStream.Length == 0 || presStream.Length == 0 || drawStream.Length == 0)
    {
        throw new InvalidOperationException("文字／簡報／繪圖文件建立失敗。");
    }

    return 3;
}

static int SmokeFormulaDocument()
{
    const string mathml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi></mrow></math>";
    using var formulaDoc = FormulaDocument.Create(mathml);
    using var stream = new MemoryStream();
    formulaDoc.SaveToStream(stream);

    if (stream.Length == 0)
    {
        throw new InvalidOperationException("公式文件建立失敗。");
    }

    return 1;
}

static int SmokeDocumentFactoryKinds()
{
    OdfDocumentKind[] kinds =
    [
        OdfDocumentKind.Text,
        OdfDocumentKind.Spreadsheet,
        OdfDocumentKind.Presentation,
        OdfDocumentKind.Graphics,
        OdfDocumentKind.Chart,
        OdfDocumentKind.Formula,
    ];

    int passed = 0;
    foreach (OdfDocumentKind kind in kinds)
    {
        using OdfDocument doc = OdfDocumentFactory.CreateDocument(kind);
        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        if (stream.Length == 0)
        {
            throw new InvalidOperationException($"OdfDocumentFactory 建立 {kind} 失敗。");
        }

        passed++;
    }

    return passed;
}

static int SmokeFormulaEvaluation()
{
    var context = new TrimSmokeEvaluationContext();
    var evaluator = new DefaultFormulaEvaluator();
    context.CellValues[OdfCellAddress.ParseExcel("A1")] = 10.0;
    context.CellValues[OdfCellAddress.ParseExcel("A2")] = 20.0;
    context.CellValues[OdfCellAddress.ParseExcel("A3")] = 30.0;

    object sum = evaluator.Evaluate("SUM(A1:A3)", context);
    if (sum is not double sumValue || Math.Abs(sumValue - 60.0) > 1e-9)
    {
        throw new InvalidOperationException($"SUM 評估失敗：{sum}");
    }

    return 1;
}

static int SmokeXmlWriterRoundTrip()
{
    OdfNode root = OdfNodeFactory.CreateElement("document", OdfNamespaces.Office);
    using var writeStream = new MemoryStream();
    OdfXmlWriter.Write(root, writeStream);

    writeStream.Position = 0;
    OdfNode parsed = OdfXmlReader.Parse(writeStream);
    if (parsed.LocalName != "document")
    {
        throw new InvalidOperationException("XmlWriter 往返失敗。");
    }

    return 1;
}

static int SmokeEmbeddedDocumentFactory()
{
    using var presDoc = PresentationDocument.Create();
    presDoc.AddSlide("Slide1");

    using OdfChartDocument chartDoc = presDoc.CreateEmbeddedDocument<OdfChartDocument>("Chart1");
    using OdfFormulaDocument formulaDoc = presDoc.CreateEmbeddedDocument<OdfFormulaDocument>("Formula1");

    using OdfChartDocument chartReloaded = presDoc.GetEmbeddedDocument<OdfChartDocument>("Chart1");
    using OdfFormulaDocument formulaReloaded = presDoc.GetEmbeddedDocument<OdfFormulaDocument>("Formula1");

    if (chartDoc is null || formulaDoc is null || chartReloaded is null || formulaReloaded is null)
    {
        throw new InvalidOperationException("嵌入式文件工廠失敗。");
    }

    return 1;
}

/// <summary>
/// TrimSmoke 專用之最小公式評估內容，避免依賴測試專案中的 Mock 類型。
/// </summary>
file sealed class TrimSmokeEvaluationContext : IEvaluationContext
{
    public OdfCellAddress CurrentCell { get; init; } = OdfCellAddress.ParseExcel("A1");

    public Dictionary<OdfCellAddress, object> CellValues { get; } = new();

    public object GetCellValue(OdfCellAddress address) =>
        CellValues.TryGetValue(address, out object? value) ? value : 0.0;

    public object[,] GetRangeValues(OdfCellRange range)
    {
        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        int rows = maxRow - minRow + 1;
        int cols = maxCol - minCol + 1;
        var matrix = new object[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var address = new OdfCellAddress(minRow + r, minCol + c, range.StartAddress.SheetName);
                matrix[r, c] = GetCellValue(address);
            }
        }

        return matrix;
    }

    public string? GetCellFormula(OdfCellAddress address) => null;

    public object GetNamedRangeOrExpressionValue(string name) => 0.0;
}