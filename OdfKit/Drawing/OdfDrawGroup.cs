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
    /// Gets the direct drawing-object children in this group.
    /// 取得此群組中的直接繪圖物件子項目。
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<OdfShape> Children
    {
        get
        {
            System.Collections.Generic.List<OdfShape> children = [];
            foreach (OdfNode child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                    children.Add(new OdfShape(child, Document));
            }
            return children.AsReadOnly();
        }
    }

    /// <summary>
    /// Finds a descendant drawing object by its draw or XML identifier.
    /// 依 draw 或 XML 識別碼尋找子孫繪圖物件。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns>The matching object, or <see langword="null"/>. / 相符的物件；若不存在則為 <see langword="null"/>。</returns>
    public OdfShape? FindShape(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        OdfNode? found = FindShapeNode(Node, id);
        return found is null ? null : new OdfShape(found, Document);
    }

    /// <summary>
    /// Removes a descendant drawing object and connectors in this group that reference it.
    /// 移除子孫繪圖物件，以及此群組中引用該物件的連接線。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveShape(string id)
    {
        OdfNode? found = FindShapeNode(Node, id);
        if (found?.Parent is null)
            return false;
        found.Parent.RemoveChild(found);
        RemoveReferencingConnectors(Node, id);
        return true;
    }

    /// <summary>
    /// Removes all direct drawing-object children from this group.
    /// 移除此群組中的所有直接繪圖物件子項目。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int Clear()
    {
        System.Collections.Generic.List<OdfNode> children = [];
        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                children.Add(child);
        }
        foreach (OdfNode child in children)
            Node.RemoveChild(child);
        return children.Count;
    }
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
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("shp_"), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }
    /// <summary>
    /// Short overload of AddConnector that accepts startShapeId and endShapeId; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 startShapeId 與 endShapeId；其餘可選參數使用預設值並轉呼叫最長 AddConnector 多載。
    /// </summary>
    public OdfShape AddConnector(string startShapeId, string endShapeId) => AddConnector(startShapeId, endShapeId, OdfConnectorType.Standard);


    /// <summary>
    /// Adds a connector within the group.
    /// 在群組內新增連接線。
    /// </summary>
    /// <param name="startShapeId">The start shape identifier. / 起點圖形識別碼。</param>
    /// <param name="endShapeId">The end shape identifier. / 終點圖形識別碼。</param>
    /// <param name="connectorType">The connector geometry type. / 連接線幾何類型。</param>
    /// <returns>The newly added connector shape instance. / 新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType)
    {
        if (string.IsNullOrEmpty(startShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawGroup_StartingCannotBeEmpty"), nameof(startShapeId));
        if (string.IsNullOrEmpty(endShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawGroup_EndCannotBeEmpty"), nameof(endShapeId));

        var connectorNode = OdfNodeFactory.CreateElement("connector", OdfNamespaces.Draw, "draw");
        connectorNode.SetAttribute("id", OdfNamespaces.Draw, global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("shp_"), "draw");
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
        frame.SetAttribute("id", OdfNamespaces.Draw, global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("frm_"), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        return frame;
    }

    private static OdfNode? FindShapeNode(OdfNode root, string id)
    {
        foreach (OdfNode child in root.Children)
        {
            string? childId = child.GetAttribute("id", OdfNamespaces.Draw) ?? child.GetAttribute("id", OdfNamespaces.Xml);
            if (string.Equals(childId, id, StringComparison.Ordinal))
                return child;
            OdfNode? descendant = FindShapeNode(child, id);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }

    private static void RemoveReferencingConnectors(OdfNode root, string id)
    {
        System.Collections.Generic.List<OdfNode> removals = [];
        foreach (OdfNode child in root.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "connector" &&
                (string.Equals(child.GetAttribute("start-shape", OdfNamespaces.Draw), id, StringComparison.Ordinal) ||
                 string.Equals(child.GetAttribute("end-shape", OdfNamespaces.Draw), id, StringComparison.Ordinal)))
                removals.Add(child);
            else
                RemoveReferencingConnectors(child, id);
        }
        foreach (OdfNode removal in removals)
            root.RemoveChild(removal);
    }
}

