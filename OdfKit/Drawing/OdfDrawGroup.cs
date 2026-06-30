using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Drawing;

/// <summary>
/// Represents an ODF drawing group.
/// 表示 ODF 繪圖群組。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">The owning ODF document instance. / 所屬的 ODF 文件執行個體。</param>
public sealed class OdfDrawGroup(OdfNode node, OdfDocument doc) : OdfShape(node, doc)
{
    /// <summary>
    /// Gets or sets the group name.
    /// 取得或設定群組名稱。
    /// </summary>
    public string? Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw);
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// Adds a text box within the group.
    /// 在群組內新增文字方塊。
    /// </summary>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <returns>The newly added text box instance. / 新增的文字方塊執行個體。</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        var textBoxNode = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, Document);
    }

    /// <summary>
    /// Adds a shape within the group.
    /// 在群組內新增圖形。
    /// </summary>
    /// <param name="shapeType">The shape type. / 圖形類型。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <returns>The newly added shape instance. / 新增的圖形執行個體。</returns>
    public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        string localName = shapeType switch
        {
            OdfShapeType.Rectangle => "rect",
            OdfShapeType.Ellipse => "ellipse",
            _ => "custom-shape"
        };

        var shapeNode = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// Adds a connector within the group.
    /// 在群組內新增連接線。
    /// </summary>
    /// <param name="startShapeId">The start shape identifier. / 起點圖形識別碼。</param>
    /// <param name="endShapeId">The end shape identifier. / 終點圖形識別碼。</param>
    /// <param name="connectorType">The connector geometry type. / 連接線幾何類型。</param>
    /// <returns>The newly added connector shape instance. / 新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(
        string startShapeId,
        string endShapeId,
        OdfConnectorType connectorType = OdfConnectorType.Standard)
    {
        if (string.IsNullOrEmpty(startShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawGroup_StartingCannotBeEmpty"), nameof(startShapeId));
        if (string.IsNullOrEmpty(endShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawGroup_EndCannotBeEmpty"), nameof(endShapeId));

        var connectorNode = OdfNodeFactory.CreateElement("connector", OdfNamespaces.Draw, "draw");
        connectorNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        connectorNode.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
        connectorNode.SetAttribute("start-shape", OdfNamespaces.Draw, startShapeId, "draw");
        connectorNode.SetAttribute("end-shape", OdfNamespaces.Draw, endShapeId, "draw");

        string typeVal = connectorType switch
        {
            OdfConnectorType.Lines => "lines",
            OdfConnectorType.Straight => "line",
            OdfConnectorType.Curve => "curve",
            _ => "standard",
        };
        connectorNode.SetAttribute("type", OdfNamespaces.Draw, typeVal, "draw");

        Node.AppendChild(connectorNode);
        return new OdfShape(connectorNode, Document);
    }

    private static OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        return frame;
    }
}

