using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定繪圖文件高階 API 的易用入口。
/// </summary>
public class DrawingApiUsabilityTests
{
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
        page.AddPicture(
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
