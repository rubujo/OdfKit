using System.Collections.Generic;

namespace OdfKit.Drawing;
/// <summary>
/// Provides the OdfDrawPage API.
/// 提供 OdfDrawPage API。
/// </summary>

public partial class OdfDrawPage
{
    /// <summary>
    /// Finds a drawing object by its draw or XML identifier.
    /// 依 draw 或 XML 識別碼查找繪圖物件。
    /// </summary>
    /// <param name="id">The drawing object identifier. / 繪圖物件識別碼。</param>
    /// <returns>The matching shape, or <see langword="null"/> when no match exists. / 符合的圖形；若找不到則為 <see langword="null"/>。</returns>
    public OdfKit.Presentation.OdfShape? FindShape(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(null, nameof(id));
        }
        OdfKit.DOM.OdfNode? node = FindShapeNode(Node, id);
        return node is null ? null : new OdfKit.Presentation.OdfShape(node, Document);
    }

    /// <summary>
    /// Removes a drawing object and connectors that reference it.
    /// 移除繪圖物件，並一併移除參照該物件的連接線。
    /// </summary>
    /// <param name="id">The drawing object identifier. / 繪圖物件識別碼。</param>
    /// <returns><see langword="true"/> if an object was removed; otherwise, <see langword="false"/>. / 若已移除物件則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveShape(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(null, nameof(id));
        }
        OdfKit.DOM.OdfNode? node = FindShapeNode(Node, id);
        if (node?.Parent is null)
        {
            return false;
        }

        node.Parent.RemoveChild(node);
        RemoveReferencingConnectors(Node, id);
        return true;
    }

    /// <summary>
    /// Gets a summary list of all path shapes on this drawing page.
    /// 取得此繪圖頁面上所有路徑圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPathInfo> GetPaths() =>
        OdfDrawPageShapeReadEngine.GetPaths(this);

    /// <summary>
    /// Gets a summary list of all connectors on this drawing page.
    /// 取得此繪圖頁面上所有連接線的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfConnectorInfo> GetConnectors() =>
        OdfDrawPageShapeReadEngine.GetConnectors(this);

    /// <summary>
    /// Gets a summary list of all polygon shapes on this drawing page.
    /// 取得此繪圖頁面上所有多邊形圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPolygonInfo> GetPolygons() =>
        OdfDrawPageShapeReadEngine.GetPolygons(this);

    /// <summary>
    /// Gets a summary list of all custom shapes on this drawing page.
    /// 取得此繪圖頁面上所有自定義幾何圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfCustomShapeInfo> GetCustomShapes() =>
        OdfDrawPageShapeReadEngine.GetCustomShapes(this);

    /// <summary>
    /// Gets a summary list of all group shapes on this drawing page.
    /// 取得此繪圖頁面上所有群組圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfGroupInfo> GetGroups() =>
        OdfDrawPageShapeReadEngine.GetGroups(this);

    /// <summary>
    /// Gets a summary list of all layers on this drawing page.
    /// 取得此繪圖頁面上所有圖層的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfLayerInfo> GetLayers() =>
        OdfDrawPageLayerReadEngine.GetLayers(this);

    /// <summary>
    /// Gets a summary list of all text boxes on this drawing page.
    /// 取得此繪圖頁面上所有文字方塊的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDrawTextBoxInfo> GetTextBoxes() =>
        OdfDrawPageShapeReadEngine.GetTextBoxes(this);

    /// <summary>
    /// Gets a summary list of all pictures on this drawing page.
    /// 取得此繪圖頁面上所有圖片的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDrawPictureInfo> GetPictures() =>
        OdfDrawPageShapeReadEngine.GetPictures(this);

    /// <summary>
    /// Gets a summary list of all shape-to-layer assignments on this drawing page.
    /// 取得此繪圖頁面上所有圖形圖層指派的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDrawShapeLayerInfo> GetShapeLayerAssignments() =>
        OdfDrawPageShapeReadEngine.GetShapeLayerAssignments(this);

    private static OdfKit.DOM.OdfNode? FindShapeNode(OdfKit.DOM.OdfNode root, string id)
    {
        foreach (OdfKit.DOM.OdfNode child in root.Children)
        {
            if (child.NodeType is not OdfKit.DOM.OdfNodeType.Element)
            {
                continue;
            }

            string? childId = child.GetAttribute("id", OdfKit.Core.OdfNamespaces.Draw)
                ?? child.GetAttribute("id", OdfKit.Core.OdfNamespaces.Xml);
            if (string.Equals(childId, id, StringComparison.Ordinal))
            {
                return child;
            }

            OdfKit.DOM.OdfNode? descendant = FindShapeNode(child, id);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void RemoveReferencingConnectors(OdfKit.DOM.OdfNode root, string shapeId)
    {
        foreach (OdfKit.DOM.OdfNode child in root.Children.ToList())
        {
            if (child.NodeType is not OdfKit.DOM.OdfNodeType.Element)
            {
                continue;
            }

            if (child.NamespaceUri == OdfKit.Core.OdfNamespaces.Draw &&
                child.LocalName == "connector" &&
                (string.Equals(child.GetAttribute("start-shape", OdfKit.Core.OdfNamespaces.Draw), shapeId, StringComparison.Ordinal) ||
                 string.Equals(child.GetAttribute("end-shape", OdfKit.Core.OdfNamespaces.Draw), shapeId, StringComparison.Ordinal)))
            {
                root.RemoveChild(child);
                continue;
            }

            RemoveReferencingConnectors(child, shapeId);
        }
    }
}
