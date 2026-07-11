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
    /// Removes all top-level drawing objects and their referencing connectors while preserving layer and unknown content.
    /// 移除所有最上層繪圖物件及其引用連接線，並保留圖層與未知內容。
    /// </summary>
    /// <returns>The number of removed top-level objects. / 已移除的最上層物件數量。</returns>
    public int ClearShapes()
    {
        List<(OdfKit.DOM.OdfNode Node, string? Id)> shapes = [];
        foreach (OdfKit.DOM.OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfKit.DOM.OdfNodeType.Element &&
                child.NamespaceUri == OdfKit.Core.OdfNamespaces.Draw &&
                child.LocalName != "layer-set")
            {
                shapes.Add((child, GetShapeId(child)));
            }
        }

        foreach ((OdfKit.DOM.OdfNode shape, _) in shapes)
            Node.RemoveChild(shape);
        foreach ((_, string? id) in shapes)
        {
            if (!string.IsNullOrEmpty(id))
                RemoveReferencingConnectors(Node, id!);
        }
        return shapes.Count;
    }

    /// <summary>
    /// Moves a drawing object to the front of its current parent stacking order.
    /// 將繪圖物件移至目前父節點堆疊順序的最前方。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool BringToFront(string id) => MoveShapeInStack(id, toFront: true);

    /// <summary>
    /// Moves a drawing object to the back of its current parent stacking order.
    /// 將繪圖物件移至目前父節點堆疊順序的最後方。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool SendToBack(string id) => MoveShapeInStack(id, toFront: false);

    /// <summary>
    /// Updates SVG path data and recomputes the path view box.
    /// 更新 SVG path data，並重新計算路徑 view box。
    /// </summary>
    /// <param name="id">The exact path identifier. / 路徑的精確識別碼。</param>
    /// <param name="svgPathData">The replacement SVG path data. / 取代用 SVG path data。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdatePathData(string id, string svgPathData)
    {
        if (svgPathData is null)
            throw new ArgumentNullException(nameof(svgPathData));
        OdfKit.DOM.OdfNode? path = FindShapeNode(Node, id);
        if (path is null || path.LocalName != "path" || path.NamespaceUri != OdfKit.Core.OdfNamespaces.Draw)
            return false;
        path.SetAttribute("d", OdfKit.Core.OdfNamespaces.Svg, svgPathData, "svg");
        path.SetAttribute("viewBox", OdfKit.Core.OdfNamespaces.Svg, ComputePathDataViewBox(svgPathData), "svg");
        return true;
    }

    /// <summary>
    /// Updates the relative point list of a polygon.
    /// 更新多邊形的相對頂點清單。
    /// </summary>
    /// <param name="id">The exact polygon identifier. / 多邊形的精確識別碼。</param>
    /// <param name="points">The replacement ODF point string. / 取代用 ODF 頂點字串。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdatePolygonPoints(string id, string points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        OdfKit.DOM.OdfNode? polygon = FindShapeNode(Node, id);
        if (polygon is null || polygon.LocalName != "polygon" || polygon.NamespaceUri != OdfKit.Core.OdfNamespaces.Draw)
            return false;
        polygon.SetAttribute("points", OdfKit.Core.OdfNamespaces.Draw, points, "draw");
        return true;
    }

    /// <summary>
    /// Updates the enhanced geometry type of a custom shape while preserving other geometry attributes and equations.
    /// 更新自訂圖形的增強幾何類型，並保留其它幾何屬性與方程式。
    /// </summary>
    /// <param name="id">The exact custom-shape identifier. / 自訂圖形的精確識別碼。</param>
    /// <param name="geometryType">The replacement geometry type. / 取代用幾何類型。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateCustomGeometryType(string id, string geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
            return false;
        OdfKit.DOM.OdfNode? shape = FindShapeNode(Node, id);
        if (shape is null || shape.LocalName != "custom-shape" || shape.NamespaceUri != OdfKit.Core.OdfNamespaces.Draw)
            return false;
        foreach (OdfKit.DOM.OdfNode child in shape.Children)
        {
            if (child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfKit.Core.OdfNamespaces.Draw)
            {
                child.SetAttribute("type", OdfKit.Core.OdfNamespaces.Draw, geometryType, "draw");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets or clears the SVG transform text of a drawing object.
    /// 設定或清除繪圖物件的 SVG transform 原文。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <param name="transform">The transform text, or <see langword="null"/> to clear it. / transform 原文；傳入 <see langword="null"/> 代表清除。</param>
    /// <returns><see langword="true"/> if the object exists; otherwise <see langword="false"/>. / 若物件存在則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool SetTransform(string id, string? transform)
    {
        OdfKit.DOM.OdfNode? shape = FindShapeNode(Node, id);
        if (shape is null)
            return false;
        if (transform is null)
            shape.RemoveAttribute("transform", OdfKit.Core.OdfNamespaces.Draw);
        else
            shape.SetAttribute("transform", OdfKit.Core.OdfNamespaces.Draw, transform, "draw");
        return true;
    }

    private bool MoveShapeInStack(string id, bool toFront)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException(null, nameof(id));
        OdfKit.DOM.OdfNode? shape = FindShapeNode(Node, id);
        OdfKit.DOM.OdfNode? parent = shape?.Parent;
        if (shape is null || parent is null)
            return false;

        OdfKit.DOM.OdfNode? firstDrawingObject = null;
        foreach (OdfKit.DOM.OdfNode sibling in parent.Children)
        {
            if (sibling.NodeType is OdfKit.DOM.OdfNodeType.Element &&
                sibling.NamespaceUri == OdfKit.Core.OdfNamespaces.Draw &&
                sibling.LocalName != "layer-set" &&
                !ReferenceEquals(sibling, shape))
            {
                firstDrawingObject = sibling;
                break;
            }
        }

        parent.RemoveChild(shape);
        if (toFront || firstDrawingObject is null)
            parent.AppendChild(shape);
        else
            parent.InsertBefore(shape, firstDrawingObject);
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
    /// Finds a layer by its exact name.
    /// 依精確名稱尋找圖層。
    /// </summary>
    /// <param name="name">The exact layer name. / 圖層的精確名稱。</param>
    /// <returns>The matching layer summary, or <see langword="null"/>. / 相符的圖層摘要；若不存在則為 <see langword="null"/>。</returns>
    public OdfLayerInfo? FindLayer(string name)
    {
        foreach (OdfLayerInfo layer in GetLayers())
        {
            if (string.Equals(layer.Name, name, StringComparison.Ordinal))
                return layer;
        }
        return null;
    }

    /// <summary>
    /// Renames a layer and updates every shape assignment on this page.
    /// 重新命名圖層，並更新此頁面上的所有圖形指派。
    /// </summary>
    /// <param name="currentName">The current exact name. / 目前的精確名稱。</param>
    /// <param name="newName">The replacement name. / 取代用名稱。</param>
    /// <returns><see langword="true"/> if renamed; otherwise <see langword="false"/>. / 若已重新命名則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RenameLayer(string currentName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || FindLayer(newName) is not null)
            return false;
        OdfKit.DOM.OdfNode? layer = FindLayerNode(currentName);
        if (layer is null)
            return false;
        layer.SetAttribute("name", OdfKit.Core.OdfNamespaces.Draw, newName, "draw");
        UpdateLayerAssignments(Node, currentName, newName);
        return true;
    }

    /// <summary>
    /// Removes a layer after redirecting its assigned shapes to a replacement layer.
    /// 將指派圖形重新導向取代圖層後，移除指定圖層。
    /// </summary>
    /// <param name="name">The exact layer name to remove. / 要移除的圖層精確名稱。</param>
    /// <param name="replacementName">The exact replacement layer name. / 取代圖層的精確名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveLayer(string name, string replacementName)
    {
        if (string.Equals(name, replacementName, StringComparison.Ordinal) || FindLayer(replacementName) is null)
            return false;
        OdfKit.DOM.OdfNode? layer = FindLayerNode(name);
        if (layer?.Parent is null)
            return false;
        UpdateLayerAssignments(Node, name, replacementName);
        layer.Parent.RemoveChild(layer);
        return true;
    }

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

    private static string? GetShapeId(OdfKit.DOM.OdfNode node) =>
        node.GetAttribute("id", OdfKit.Core.OdfNamespaces.Draw) ??
        node.GetAttribute("id", OdfKit.Core.OdfNamespaces.Xml);

    private OdfKit.DOM.OdfNode? FindLayerNode(string name)
    {
        OdfKit.DOM.OdfNode? result = FindLayerNodeIn(Node, name);
        return result ?? (Node.Parent is null ? null : FindLayerNodeIn(Node.Parent, name));
    }

    private static OdfKit.DOM.OdfNode? FindLayerNodeIn(OdfKit.DOM.OdfNode container, string name)
    {
        foreach (OdfKit.DOM.OdfNode child in container.Children)
        {
            if (child.NodeType is not OdfKit.DOM.OdfNodeType.Element ||
                child.LocalName != "layer-set" ||
                child.NamespaceUri != OdfKit.Core.OdfNamespaces.Draw)
                continue;
            foreach (OdfKit.DOM.OdfNode layer in child.Children)
            {
                if (layer.NodeType is OdfKit.DOM.OdfNodeType.Element &&
                    layer.LocalName == "layer" &&
                    layer.NamespaceUri == OdfKit.Core.OdfNamespaces.Draw &&
                    string.Equals(layer.GetAttribute("name", OdfKit.Core.OdfNamespaces.Draw), name, StringComparison.Ordinal))
                    return layer;
            }
        }
        return null;
    }

    private static void UpdateLayerAssignments(OdfKit.DOM.OdfNode root, string currentName, string newName)
    {
        foreach (OdfKit.DOM.OdfNode child in root.Children)
        {
            if (string.Equals(child.GetAttribute("layer", OdfKit.Core.OdfNamespaces.Draw), currentName, StringComparison.Ordinal))
                child.SetAttribute("layer", OdfKit.Core.OdfNamespaces.Draw, newName, "draw");
            UpdateLayerAssignments(child, currentName, newName);
        }
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
