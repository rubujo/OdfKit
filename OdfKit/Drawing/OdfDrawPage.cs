using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示 ODF 繪圖頁面（Drawing Page）的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的繪圖文件執行個體</param>
public partial class OdfDrawPage(OdfNode node, DrawingDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的繪圖文件。
    /// </summary>
    public DrawingDocument Document { get; } = doc;

    /// <summary>
    /// 取得或設定繪圖頁面的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定繪圖頁面所使用的母片名稱。
    /// </summary>
    public string? MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
        set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 取得繪圖頁面上的文字方塊清單。
    /// </summary>
    public IReadOnlyList<OdfTextBox> TextBoxes => FindDrawingObjects(
        node => ContainsDescendant(node, "text-box", OdfNamespaces.Draw),
        node => new OdfTextBox(node, Document));

    /// <summary>
    /// 取得繪圖頁面上的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfPicture> Pictures => FindDrawingObjects(
        node => ContainsDescendant(node, "image", OdfNamespaces.Draw),
        node => new OdfPicture(node, Document));

    /// <summary>
    /// 取得繪圖頁面上的一般圖形清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            node.LocalName is "rect" or "ellipse" or "custom-shape" or "line" or "connector" or "polyline" or "g",
        node => new OdfShape(node, Document));
}
