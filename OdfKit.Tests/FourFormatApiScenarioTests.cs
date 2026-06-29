using System;
using System.IO;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證四主格式（ODT／ODS／ODP／ODG）Wave 2 高階 API 的整合場景。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Scenario)]
public class FourFormatApiScenarioTests
{
    private const string DublinCoreTitle = OdfNamespaces.Dc + "title";

    /// <summary>
    /// 驗證 ODT 追蹤修訂讀取 API 可於完整建立／儲存／載入流程後列舉修訂。
    /// </summary>
    [Fact]
    public void TextScenario_TrackedChangesReadableAfterRoundTrip()
    {
        using var stream = new MemoryStream();
        string changeId;
        using (TextDocument document = TextDocument.Create())
        {
            document.TrackedChanges = true;
            document.AddParagraph(string.Empty).AddTextRun("場景修訂文字");
            changeId = document.GetTrackedChanges()
                .First(candidate => candidate.Content == "場景修訂文字")
                .RegionId;
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using TextDocument loaded = TextDocument.Load(stream);

        OdfTrackedChange change = loaded.GetTrackedChanges()
            .First(candidate => candidate.RegionId == changeId);
        Assert.Equal(OdfChangeType.Insertion, change.ChangeType);
        Assert.Equal("場景修訂文字", change.Content);
    }

    /// <summary>
    /// 驗證 ODS 資料驗證、嵌入圖表與追蹤修訂讀取 API 可於單一工作流程後讀回。
    /// </summary>
    [Fact]
    public void SpreadsheetScenario_ReadApisSurviveRoundTrip()
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.Worksheets.Add("銷售");
        workbook.AddDataValidation("銷售", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 0, 0, "銷售"),
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "99",
            ErrorMessage = "請輸入 1 至 99",
        });
        workbook.AddChart("銷售", new OdfCellAddress(0, 2, "銷售"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "趨勢",
            DataRange = new OdfCellRange(0, 0, 3, 1, "銷售"),
        });
        workbook.TrackedChanges = false;
        workbook.Worksheets["銷售"].Cells["A1"].CellValue = "100";
        workbook.TrackedChanges = true;
        workbook.Worksheets["銷售"].Cells["A1"].CellValue = "120";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);

        OdfDataValidationInfo validation = Assert.Single(loaded.GetDataValidations());
        Assert.True(validation.TryGetCondition(out OdfValidationCondition condition));
        Assert.Equal(OdfValidationCondition.IntegerBetween, condition);

        OdfEmbeddedChartInfo chart = Assert.Single(loaded.GetEmbeddedCharts());
        Assert.Equal("趨勢", chart.Title);
        Assert.Equal(OdfChartType.Line, chart.ChartType);

        OdfSpreadsheetTrackedChangeInfo change = Assert.Single(loaded.GetTrackedChanges());
        Assert.Equal("100", change.PreviousContent);
        Assert.Equal("120", loaded.Worksheets["銷售"].Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證 ODP 動畫讀取 API 可於簡報建立／儲存／載入流程後列舉效果。
    /// </summary>
    [Fact]
    public void PresentationScenario_AnimationsReadableAfterRoundTrip()
    {
        using var document = PresentationDocument.Create();
        OdfSlide slide = document.AddSlide();
        OdfPlaceholder placeholder = slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("10.0cm"),
            OdfLength.Parse("2.0cm"));
        string shapeId = placeholder.Id;
        slide.AddEntranceEffect(shapeId, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick, TimeSpan.FromSeconds(0.5));
        slide.AddExitEffect(shapeId, OdfAnimationEffect.Zoom, OdfAnimationTrigger.WithPrevious, TimeSpan.FromSeconds(1.0));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using PresentationDocument loaded = PresentationDocument.Load(stream);
        OdfSlide loadedSlide = loaded.Slides[0];
        Assert.Equal(2, loadedSlide.GetAnimations().Count);
        Assert.Contains(
            loadedSlide.GetAnimations(),
            animation => animation.Kind == OdfAnimationKind.Entrance && animation.TargetElementId == shapeId);
        Assert.Contains(
            loadedSlide.GetAnimations(),
            animation => animation.Kind == OdfAnimationKind.Exit && animation.Effect == OdfAnimationEffect.Zoom);
    }

    /// <summary>
    /// 驗證 ODG 路徑、連接線、多邊形與自定義圖形讀取 API 可於單一繪圖流程後讀回。
    /// </summary>
    [Fact]
    public void DrawingScenario_ShapeReadApisSurviveRoundTrip()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("場景頁");
        document.AddPath(
            "M 10 10 L 20 20 Z",
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("10.0cm"),
            OdfLength.Parse("5.0cm"));
        OdfShape startShape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("3cm"),
            OdfLength.Parse("2cm"));
        OdfShape endShape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.Parse("10cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("3cm"),
            OdfLength.Parse("2cm"));
        document.AddConnector(startShape.Id, endShape.Id, OdfConnectorType.Curve);
        document.AddPolygon(
        [
            (OdfLength.Parse("2.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("12.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("7.0cm"), OdfLength.Parse("7.0cm")),
        ]);
        document.AddCustomShape(
            "smiley",
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("5.0cm"),
            OdfLength.Parse("5.0cm"));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using DrawingDocument loaded = DrawingDocument.Load(stream);
        OdfDrawPage loadedPage = loaded.Pages[0];

        Assert.Single(loadedPage.GetPaths());
        Assert.Equal("M 10 10 L 20 20 Z", loadedPage.GetPaths()[0].SvgPathData);

        OdfConnectorInfo connector = Assert.Single(loadedPage.GetConnectors());
        Assert.Equal(startShape.Id, connector.StartShapeId);
        Assert.Equal(endShape.Id, connector.EndShapeId);

        Assert.Single(loadedPage.GetPolygons());
        Assert.Single(loadedPage.GetCustomShapes());
        Assert.Equal("smiley", loadedPage.GetCustomShapes()[0].GeometryType);
    }

    /// <summary>
    /// 驗證 ODP 儲存／載入後可列舉投影片母片。
    /// </summary>
    [Fact]
    public void PresentationScenario_GetMasterPagesSurvivesRoundTrip()
    {
        using var document = PresentationDocument.Create();
        document.AddSlide();
        document.AddMasterPage("SceneMaster", new OdfMasterPageDefinition { BackgroundColor = "#112233" });

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using PresentationDocument loaded = PresentationDocument.Load(stream);
        OdfMasterPage master = Assert.Single(loaded.GetMasterPages());
        Assert.Equal("SceneMaster", master.Name);
    }

    /// <summary>
    /// 驗證 ODS 資料庫範圍讀取 API 可於儲存／載入後讀回篩選條件。
    /// </summary>
    [Fact]
    public void SpreadsheetScenario_GetDatabaseRangesSurvivesRoundTrip()
    {
        using var workbook = SpreadsheetDocument.Create();
        workbook.AddSheet("資料");
        OdfDatabaseRange range = workbook.AddDatabaseRange(
            "清單",
            new OdfCellRange(new OdfCellAddress(0, 0, "資料"), new OdfCellAddress(4, 1, "資料")));
        range.SetFilter((0, "=", "完成"));

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfDatabaseRangeInfo info = Assert.Single(loaded.GetDatabaseRanges());
        Assert.Equal("清單", info.Name);
        Assert.Single(info.FilterConditions);
        Assert.Equal("完成", info.FilterConditions[0].Value);
    }

    /// <summary>
    /// 驗證 ODT 頁首頁尾進階寫入 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void TextScenario_HeaderFooterAdvancedEditingSurvivesRoundTrip()
    {
        using var stream = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.AddPageStyle("Landscape");
            OdfPageSetup landscape = document.GetPageSetup("Landscape");
            landscape.HeaderFirst.Text = "首頁專用頁首";
            landscape.FooterMinHeight = "0.9cm";

            OdfParagraph footerParagraph = document.GetDefaultPageSetup().Footer.GetOrCreateParagraph();
            footerParagraph.TextContent = "第 ";
            document.GetDefaultPageSetup().Footer.AddPageNumberField();

            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using TextDocument loaded = TextDocument.Load(stream);

        OdfPageSetupInfo landscapeInfo = Assert.Single(
            loaded.GetPageSetups(),
            setup => setup.Name == "Landscape");
        Assert.Equal("首頁專用頁首", landscapeInfo.HeaderFirstText);

        OdfPageSetupInfo standardInfo = Assert.Single(
            loaded.GetPageSetups(),
            setup => setup.Name == "Standard");
        Assert.Equal("第 ", standardInfo.FooterText);
    }

    /// <summary>
    /// 驗證 ODS 圖表軸與序列進階寫入 API 可於嵌入圖表往返後讀回。
    /// </summary>
    [Fact]
    public void SpreadsheetScenario_ChartAdvancedEditingSurvivesRoundTrip()
    {
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.AddSheet("銷售");
        sheet.GetCell(0, 0).SetValue("季度");
        sheet.GetCell(1, 0).SetValue("Q1");
        sheet.GetCell(2, 0).SetValue("Q2");
        sheet.GetCell(0, 1).SetValue("銷售額");
        sheet.GetCell(1, 1).SetValue(100d);
        sheet.GetCell(2, 1).SetValue(200d);

        OdfChartDocument chartDoc = sheet.InsertChart(
            new OdfCellRange(0, 0, 2, 1, "銷售"),
            OdfChartType.Bar);
        chartDoc.ChartTitle = "季度銷售";
        chartDoc.SetAxisMaximum("y", 500);
        chartDoc.SetAxisGrid("y", OdfChartGridKind.Major, true);
        if (chartDoc.SeriesCount > 0)
            chartDoc.GetSeriesEditor(0).SeriesClass = "chart:bar";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);
        OdfChartDocument reloadedChart = loaded.GetEmbeddedChartDocument(Assert.Single(loaded.GetEmbeddedCharts()));
        OdfChartAxisInfo? axisInfo = reloadedChart.FindAxisInfo("y");
        Assert.NotNull(axisInfo);
        Assert.Equal(500, axisInfo!.Maximum);
        Assert.True(axisInfo.HasMajorGrid);
        if (reloadedChart.SeriesCount > 0)
            Assert.Equal("chart:bar", reloadedChart.Series[0].SeriesClass);
    }

    /// <summary>
    /// 驗證 ODP 母片與版面配置進階寫入 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void PresentationScenario_MasterAndLayoutAdvancedEditingSurvivesRoundTrip()
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create())
        {
            document.AddSlide();
            document.AddMasterPage("BrandMaster", new OdfMasterPageDefinition { Name = "BrandMaster" });
            OdfMasterPage master = document.GetMasterPage("BrandMaster");
            master.BackgroundColor = "#445566";

            OdfPresentationPageLayout layout = document.CreatePresentationPageLayout("SceneLayout");
            layout.AddPlaceholder(
                OdfPlaceholderType.Title,
                OdfLength.Parse("2cm"), OdfLength.Parse("1.5cm"),
                OdfLength.Parse("20cm"), OdfLength.Parse("3cm"));

            document.Slides[0].SetMasterPage("BrandMaster");
            document.ApplyPresentationPageLayout(0, "SceneLayout");
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using PresentationDocument loaded = PresentationDocument.Load(stream);

        Assert.Equal("#445566", loaded.GetMasterPage("BrandMaster").BackgroundColor);
        Assert.Equal("BrandMaster", loaded.Slides[0].MasterPageName);
        Assert.Equal("SceneLayout", loaded.Slides[0].PresentationPageLayoutName);
        Assert.Single(loaded.Slides[0].Placeholders);
    }

    /// <summary>
    /// 驗證 ODG 群組與連接線路由進階寫入 API 可於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void DrawingScenario_GroupAndConnectorAdvancedEditingSurvivesRoundTrip()
    {
        const string routePoints = "0cm 0cm 2cm 1cm 4cm 0cm";
        using var stream = new MemoryStream();
        string? groupName;
        using (var document = DrawingDocument.Create())
        {
            OdfDrawPage page = document.AddPage("場景頁");
            OdfShape first = page.AddShape(
                OdfShapeType.Rectangle,
                OdfLength.Parse("1cm"),
                OdfLength.Parse("1cm"),
                OdfLength.Parse("3cm"),
                OdfLength.Parse("2cm"));
            OdfShape second = page.AddShape(
                OdfShapeType.Rectangle,
                OdfLength.Parse("6cm"),
                OdfLength.Parse("1cm"),
                OdfLength.Parse("3cm"),
                OdfLength.Parse("2cm"));
            OdfDrawGroup group = page.GroupShapes([first.Id, second.Id], "流程群組");
            groupName = group.Name;

            OdfShape startShape = page.AddShape(
                OdfShapeType.Rectangle,
                OdfLength.Parse("1cm"),
                OdfLength.Parse("5cm"),
                OdfLength.Parse("2cm"),
                OdfLength.Parse("1cm"));
            OdfShape endShape = page.AddShape(
                OdfShapeType.Rectangle,
                OdfLength.Parse("8cm"),
                OdfLength.Parse("5cm"),
                OdfLength.Parse("2cm"),
                OdfLength.Parse("1cm"));
            OdfShape connector = document.AddConnector(startShape.Id, endShape.Id, OdfConnectorType.Standard);
            connector.SetConnectorRoutePoints(routePoints);

            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using DrawingDocument loaded = DrawingDocument.Load(stream);
        OdfDrawPage loadedPage = loaded.Pages[0];

        OdfGroupInfo groupInfo = Assert.Single(loadedPage.GetGroups(), group => group.Name == groupName);
        Assert.Equal(groupName, groupInfo.Name);

        OdfConnectorInfo connectorInfo = Assert.Single(loadedPage.GetConnectors());
        Assert.Equal(routePoints, connectorInfo.Points);
    }

    /// <summary>
    /// 驗證 ODT 儲存／載入會保留自訂 RDF triple 並同步標準 <c>pkg:</c> ontology。
    /// </summary>
    [Fact]
    public void TextScenario_RdfMetadataAndPkgOntologySurviveRoundTrip()
    {
        const string title = "RDF 場景標題";
        using var stream = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.Package.RdfMetadata.AddTriple(string.Empty, DublinCoreTitle, title);
            document.Package.RdfMetadata.LinkDocumentPart(string.Empty, "content.xml");
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using TextDocument loaded = TextDocument.Load(stream);

        Assert.True(loaded.Package.HasEntry("META-INF/manifest.rdf"));
        Assert.True(loaded.Package.RdfMetadata.TryGetLiteral(string.Empty, DublinCoreTitle, out string loadedTitle));
        Assert.Equal(title, loadedTitle);
        Assert.Contains("content.xml", loaded.Package.RdfMetadata.GetLinkedPartPaths(string.Empty));
        Assert.True(
            loaded.Package.RdfMetadata.TryGetLiteral("content.xml", OdfPkgRdfPredicates.MimeType, out string mimeType));
        Assert.Equal("text/xml", mimeType);
    }
}
