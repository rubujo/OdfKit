using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Export;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定繪圖文件高階 API 的易用入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class DrawingApiUsabilityTests
{
    /// <summary>
    /// 驗證繪圖 Fluent builder 可建立頁面、圖形與文字方塊。
    /// </summary>
    [Fact]
    public void DrawingDocumentBuilderCreatesPageShapesAndTextBox()
    {
        using DrawingDocument drawing = DrawingDocument.Builder()
            .WithMetadata(metadata => metadata.Title("流程圖草稿"))
            .AddPage("主畫布", page => page
                .AddRectangle(1, 1, 4, 2)
                .AddTextBox("開始", 1, 4, 3, 1)
                .AddPath("M 0 0 L 2 2 Z", 6, 1, 3, 3))
            .Build();

        using var stream = new MemoryStream();
        drawing.SaveToStream(stream);
        stream.Position = 0;

        using DrawingDocument loaded = DrawingDocument.Load(stream);

        Assert.Equal("流程圖草稿", loaded.Title);
        Assert.Equal("主畫布", loaded.Pages[0].Name);
        Assert.Single(loaded.Pages[0].Shapes, shape => shape.LocalName == "rect");
        Assert.Equal("開始", loaded.Pages[0].TextBoxes[0].Text);
        Assert.Contains(loaded.GetPaths(), path => path.SvgPathData == "M 0 0 L 2 2 Z");
    }

    /// <summary>
    /// 驗證繪圖 Fluent builder 可建立流程圖常見的圖層、連接線、群組與圖片。
    /// </summary>
    [Fact]
    public void DrawingDocumentBuilderCreatesComplexFlowDiagram()
    {
        using DrawingDocument drawing = DrawingDocument.Builder()
            .WithMetadata(metadata => metadata.Title("匯入流程"))
            .WithTheme(OdfDesignTheme.Flowchart)
            .WithStyles(OdfStyleSet.BusinessReport)
            .WithLayoutPreset(OdfLayoutPreset.FlowDiagram)
            .AddPage("主流程", page => page
                .AddLayer("背景", isProtected: true)
                .AddLayer("流程")
                .AddFlowStep("load", "載入 ODF", 0, configure: shape => shape
                    .WithId("load")
                    .OnLayer("流程"))
                .AddFlowStep("validate", "驗證封裝", 1, OdfShapeType.Ellipse, shape => shape
                    .WithId("validate")
                    .OnLayer("流程"))
                .AddFlowStep("export", "輸出報告", 2, configure: shape => shape
                    .WithId("export")
                    .OnLayer("流程"))
                .AddConnector("load", "validate", OdfConnectorType.Straight)
                .AddConnector("validate", "export", OdfConnectorType.Straight)
                .AddImage(CreatePngBytes(), 1, 4, 1, 1)
                .AddGroup("圖例", group => group
                    .AddRectangle(13, 4, 1, 1, shape => shape.Fill("#d9ead3"))
                    .AddTextBox("完成節點", 14.2, 4, 3, 1)))
            .Build();

        using var stream = new MemoryStream();
        drawing.SaveToStream(stream);
        stream.Position = 0;

        using DrawingDocument loaded = DrawingDocument.Load(stream, "flow.odg");

        Assert.Equal("匯入流程", loaded.Title);
        Assert.Equal("主流程", loaded.Pages[0].Name);
        Assert.Contains(loaded.GetLayers(), layer => layer.Name == "背景" && layer.IsProtected);
        Assert.Contains(loaded.GetShapeLayerAssignments(), assignment => assignment.Id == "load" && assignment.LayerName == "流程");
        Assert.Equal("#D9EAF7", loaded.Pages[0].Shapes.Single(shape => shape.Id == "load").FillColor);
        Assert.Equal("#FFF2CC", loaded.Pages[0].Shapes.Single(shape => shape.Id == "validate").FillColor);
        Assert.Equal("#D9EAD3", loaded.Pages[0].Shapes.Single(shape => shape.Id == "export").FillColor);
        Assert.Equal("#1F4E79", loaded.Pages[0].Shapes.Single(shape => shape.Id == "load").StrokeColor);
        Assert.Equal("#1F4E79", loaded.Pages[0].Shapes.First(shape => shape.LocalName == "connector").StrokeColor);
        Assert.Contains(loaded.GetConnectors(), connector => connector.StartShapeId == "load" && connector.EndShapeId == "validate");
        Assert.Contains(loaded.Pages[0].Pictures, picture => picture.ImageHref is { } href && href.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "g");
        Assert.Contains(loaded.Pages[0].TextBoxes, textBox => textBox.Text == "輸出報告");

        string svg = loaded.ToSvg();
        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("載入 ODF", svg, StringComparison.Ordinal);
        Assert.Contains("image", svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證內建設計主題每次都回傳獨立執行個體，避免客製化污染後續 builder。
    /// </summary>
    [Fact]
    public void BuiltInDesignThemeInstancesAreIsolated()
    {
        OdfDesignTheme.Flowchart.WithAccentFillColors("#000000");

        OdfDesignTheme freshTheme = OdfDesignTheme.Flowchart;

        Assert.Equal("#CFE2F3", freshTheme.GetAccentFillColor(0));
    }

    /// <summary>
    /// 驗證可用 pages collection 建立常見 ODG 內容並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadPagesShapesPicturesGroupsAndConnectors()
    {
        using var drawing = DrawingDocument.Create();
        OdfDrawPage page = drawing.Pages.Add("Canvas");

        page.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(2),
            "流程圖");
        OdfShape rect = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        rect.FillColor = "#ffcc00";
        rect.StrokeColor = "#333333";

        page.AddShape(
            OdfShapeType.Ellipse,
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        page.AddLine(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(7),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(7));
        page.AddConnector(
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(5));
        OdfPicture picture = page.AddPicture(
            CreatePngBytes(),
            OdfLength.FromCentimeters(9),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));

        OdfDrawGroup group = page.AddGroup("群組");
        group.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(9),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(1));
        group.AddTextBox(
            OdfLength.FromCentimeters(9),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(1),
            "群組文字");

        using var stream = new MemoryStream();
        drawing.SaveToStream(stream);
        stream.Position = 0;

        using DrawingDocument loaded = DrawingDocument.Load(stream, "drawing.odg");
        OdfNode loadedPage = loaded.Pages[0].Node;

        Assert.Single(loaded.Pages);
        Assert.Equal("Canvas", loaded.Pages[0].Name);
        Assert.Equal("application/vnd.oasis.opendocument.graphics", loaded.Package.MimeType);
        Assert.True(loaded.Package.HasEntry("Pictures/drawing_image.png"));
        Assert.Equal("流程圖", loaded.Pages[0].TextBoxes[0].Text);
        Assert.Equal(picture.ImageHref, loaded.Pages[0].Pictures[0].ImageHref);
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "rect");
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "ellipse");
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "line");
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "connector");
        Assert.Contains(loaded.Pages[0].Shapes, shape => shape.LocalName == "g");
        Assert.True(HasElement(loadedPage, "rect", OdfNamespaces.Draw));
        Assert.True(HasElement(loadedPage, "ellipse", OdfNamespaces.Draw));
        Assert.True(HasElement(loadedPage, "line", OdfNamespaces.Draw));
        Assert.True(HasElement(loadedPage, "connector", OdfNamespaces.Draw));
        Assert.True(HasElement(loadedPage, "image", OdfNamespaces.Draw));
        Assert.True(HasElement(loadedPage, "g", OdfNamespaces.Draw));
        Assert.Equal("#ffcc00", new OdfShape(FindElement(loadedPage, "rect", OdfNamespaces.Draw)!, loaded).FillColor);
    }

    /// <summary>
    /// 驗證非 ODG 文件不會被誤載為繪圖。
    /// </summary>
    [Fact]
    public void LoadRejectsNonDrawingDocument()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidOperationException>(() => DrawingDocument.Load(stream, "text.odt"));
    }

    /// <summary>
    /// 驗證圖形可標記為 ODF 1.4 裝飾性物件。
    /// </summary>
    [Fact]
    public void ShapeMarkAsDecorative_WritesDrawDecorativeAttribute()
    {
        using var drawing = DrawingDocument.Create();
        OdfDrawPage page = drawing.Pages.Add("Canvas");
        page.AddShape(OdfShapeType.Rectangle, 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm())
            .MarkAsDecorative();

        using var stream = new MemoryStream();
        drawing.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("draw:decorative=\"true\"", xml);
    }

    /// <summary>
    /// 驗證圖形框架可嵌入表格。
    /// </summary>
    [Fact]
    public void ShapeEmbeddedTable_WritesTableInsideShape()
    {
        using var drawing = DrawingDocument.Create();
        OdfDrawPage page = drawing.Pages.Add("Canvas");
        page.AddTextBox(1.Cm(), 1.Cm(), 4.Cm(), 2.Cm(), string.Empty)
            .AddEmbeddedTable(1, 2)
            .SetCellText(0, 0, "欄一")
            .SetCellText(0, 1, "欄二");

        using var stream = new MemoryStream();
        drawing.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("draw:text-box", xml);
        Assert.Contains("table:table", xml);
        Assert.Contains("table:table-row", xml);
        Assert.Contains("table:table-cell", xml);
        Assert.Contains("欄一", xml);
        Assert.Contains("欄二", xml);
    }

    private static bool HasElement(OdfNode node, string localName, string namespaceUri)
    {
        return FindElement(node, localName, namespaceUri) is not null;
    }

    private static OdfNode? FindElement(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindElement(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }
}
