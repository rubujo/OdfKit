using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;

namespace OdfKit.Drawing;

/// <summary>
/// Provides named drawing appearance resources.
/// 提供具名稱的繪圖外觀資源。
/// </summary>
public partial class DrawingDocument
{
    /// <summary>
    /// Gets all named line-marker definitions.
    /// 取得所有具名稱的線段標記定義。
    /// </summary>
    public IReadOnlyList<OdfMarker> Markers
    {
        get
        {
            var markers = new List<OdfMarker>();
            CollectMarkers(ContentRoot, markers);
            CollectMarkers(StylesRoot, markers);
            return markers.AsReadOnly();
        }
    }

    /// <summary>
    /// Adds or updates a named line-marker definition.
    /// 新增或更新具名稱的線段標記定義。
    /// </summary>
    /// <param name="name">The marker name. / 標記名稱。</param>
    /// <param name="viewBox">The SVG view box. / SVG 檢視方塊。</param>
    /// <param name="pathData">The SVG path data. / SVG 路徑資料。</param>
    /// <returns>The created or updated marker. / 已建立或更新的標記。</returns>
    public OdfMarker SetMarker(string name, string viewBox, string pathData)
    {
        OdfMarker? marker = FindMarker(name);
        if (marker is null)
        {
            OdfNode node = new(OdfNodeType.Element, "marker", OdfNamespaces.Draw, "draw");
            node.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
            FindOrCreateAutomaticStyles().AppendChild(node);
            marker = new OdfMarker(node);
        }
        marker.ViewBox = viewBox;
        marker.PathData = pathData;
        return marker;
    }

    /// <summary>
    /// Finds a named line-marker definition.
    /// 尋找具名稱的線段標記定義。
    /// </summary>
    /// <param name="name">The exact marker name. / 精確的標記名稱。</param>
    /// <returns>The matching marker, or <see langword="null"/>. / 相符的標記；若不存在則為 <see langword="null"/>。</returns>
    public OdfMarker? FindMarker(string name)
    {
        foreach (OdfMarker marker in Markers)
        {
            if (string.Equals(marker.Name, name, StringComparison.Ordinal))
                return marker;
        }
        return null;
    }

    /// <summary>
    /// Renames a marker and updates shape references.
    /// 重新命名標記，並更新圖形參照。
    /// </summary>
    /// <param name="name">The current marker name. / 目前的標記名稱。</param>
    /// <param name="newName">The new marker name. / 新標記名稱。</param>
    /// <returns><see langword="true"/> if renamed; otherwise <see langword="false"/>. / 若已重新命名則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RenameMarker(string name, string newName)
    {
        OdfMarker? marker = FindMarker(name);
        if (marker is null || FindMarker(newName) is not null)
            return false;
        foreach (OdfShape shape in FindShapesUsingMarker(name))
        {
            if (string.Equals(shape.MarkerStartName, name, StringComparison.Ordinal))
                shape.MarkerStartName = newName;
            if (string.Equals(shape.MarkerEndName, name, StringComparison.Ordinal))
                shape.MarkerEndName = newName;
        }
        marker.Node.SetAttribute("name", OdfNamespaces.Draw, newName, "draw");
        return true;
    }

    /// <summary>
    /// Removes an unreferenced named marker.
    /// 移除未被參照的具名稱標記。
    /// </summary>
    /// <param name="name">The exact marker name. / 精確的標記名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveMarker(string name)
    {
        OdfMarker? marker = FindMarker(name);
        if (marker?.Node.Parent is null || FindShapesUsingMarker(name).Count > 0)
            return false;
        return marker.Node.Parent.RemoveChild(marker.Node);
    }

    /// <summary>
    /// Removes all unreferenced marker definitions.
    /// 移除所有未被參照的標記定義。
    /// </summary>
    /// <returns>The number of removed markers. / 已移除的標記數量。</returns>
    public int ClearMarkers()
    {
        List<OdfMarker> markers = [.. Markers];
        int removed = 0;
        foreach (OdfMarker marker in markers)
        {
            if (RemoveMarker(marker.Name))
                removed++;
        }
        return removed;
    }

    /// <summary>
    /// Gets all named gradient definitions.
    /// 取得所有具名稱的漸層定義。
    /// </summary>
    public IReadOnlyList<OdfGradient> Gradients
    {
        get
        {
            var gradients = new List<OdfGradient>();
            CollectGradients(ContentRoot, gradients);
            CollectGradients(StylesRoot, gradients);
            return gradients.AsReadOnly();
        }
    }

    /// <summary>
    /// Adds or updates a named gradient definition.
    /// 新增或更新具名稱的漸層定義。
    /// </summary>
    /// <param name="name">The gradient name. / 漸層名稱。</param>
    /// <param name="style">The gradient style token. / 漸層樣式詞彙。</param>
    /// <param name="startColor">The starting color. / 起始色彩。</param>
    /// <param name="endColor">The ending color. / 結束色彩。</param>
    /// <param name="angle">The angle in tenths of a degree. / 以十分之一度為單位的角度。</param>
    /// <returns>The created or updated gradient. / 已建立或更新的漸層。</returns>
    public OdfGradient SetGradient(string name, string style, string startColor, string endColor, int angle)
    {
        OdfGradient? gradient = FindGradient(name);
        if (gradient is null)
        {
            OdfNode node = new(OdfNodeType.Element, "gradient", OdfNamespaces.Draw, "draw");
            node.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
            FindOrCreateAutomaticStyles().AppendChild(node);
            gradient = new OdfGradient(node);
        }

        gradient.Style = style;
        gradient.StartColor = startColor;
        gradient.EndColor = endColor;
        gradient.Angle = angle;
        return gradient;
    }

    /// <summary>
    /// Finds a named gradient definition.
    /// 尋找具名稱的漸層定義。
    /// </summary>
    /// <param name="name">The exact gradient name. / 精確的漸層名稱。</param>
    /// <returns>The matching gradient, or <see langword="null"/>. / 相符的漸層；若不存在則為 <see langword="null"/>。</returns>
    public OdfGradient? FindGradient(string name)
    {
        foreach (OdfGradient gradient in Gradients)
        {
            if (string.Equals(gradient.Name, name, StringComparison.Ordinal))
                return gradient;
        }

        return null;
    }

    /// <summary>
    /// Renames a gradient and updates shape references.
    /// 重新命名漸層，並更新圖形參照。
    /// </summary>
    /// <param name="name">The current gradient name. / 目前的漸層名稱。</param>
    /// <param name="newName">The new gradient name. / 新漸層名稱。</param>
    /// <returns><see langword="true"/> if renamed; otherwise <see langword="false"/>. / 若已重新命名則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RenameGradient(string name, string newName)
    {
        OdfGradient? gradient = FindGradient(name);
        if (gradient is null || FindGradient(newName) is not null)
            return false;

        foreach (OdfShape shape in FindShapesUsingGradient(name))
            shape.FillGradientName = newName;
        gradient.Node.SetAttribute("name", OdfNamespaces.Draw, newName, "draw");
        return true;
    }

    /// <summary>
    /// Removes an unreferenced named gradient.
    /// 移除未被參照的具名稱漸層。
    /// </summary>
    /// <param name="name">The exact gradient name. / 精確的漸層名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveGradient(string name)
    {
        OdfGradient? gradient = FindGradient(name);
        if (gradient?.Node.Parent is null || FindShapesUsingGradient(name).Count > 0)
            return false;
        return gradient.Node.Parent.RemoveChild(gradient.Node);
    }

    /// <summary>
    /// Removes all unreferenced gradient definitions.
    /// 移除所有未被參照的漸層定義。
    /// </summary>
    /// <returns>The number of removed gradients. / 已移除的漸層數量。</returns>
    public int ClearGradients()
    {
        List<OdfGradient> gradients = [.. Gradients];
        int removed = 0;
        foreach (OdfGradient gradient in gradients)
        {
            if (RemoveGradient(gradient.Name))
                removed++;
        }
        return removed;
    }

    private OdfNode FindOrCreateAutomaticStyles()
    {
        foreach (OdfNode child in ContentRoot.Children)
        {
            if (child.LocalName == "automatic-styles" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }

        var automaticStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        OdfNode? body = null;
        foreach (OdfNode child in ContentRoot.Children)
        {
            if (child.LocalName == "body" && child.NamespaceUri == OdfNamespaces.Office)
            {
                body = child;
                break;
            }
        }
        if (body is null)
            ContentRoot.AppendChild(automaticStyles);
        else
            ContentRoot.InsertBefore(automaticStyles, body);
        return automaticStyles;
    }

    private static void CollectGradients(OdfNode root, List<OdfGradient> gradients)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "gradient" && child.NamespaceUri == OdfNamespaces.Draw)
                gradients.Add(new OdfGradient(child));
            else
                CollectGradients(child, gradients);
        }
    }

    private static void CollectMarkers(OdfNode root, List<OdfMarker> markers)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "marker" && child.NamespaceUri == OdfNamespaces.Draw)
                markers.Add(new OdfMarker(child));
            else
                CollectMarkers(child, markers);
        }
    }

    private List<OdfShape> FindShapesUsingGradient(string name)
    {
        var shapes = new List<OdfShape>();
        foreach (OdfNode node in ContentRoot.Descendants())
        {
            if (node.NodeType != OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Draw)
                continue;
            string? styleName = node.GetAttribute("style-name", OdfNamespaces.Draw);
            if (string.IsNullOrEmpty(styleName))
                continue;
            string? gradientName = StyleEngine.GetStyleProperty(styleName!, "fill-gradient-name", OdfNamespaces.Draw, "graphic");
            if (string.Equals(gradientName, name, StringComparison.Ordinal))
                shapes.Add(new OdfShape(node, this));
        }
        return shapes;
    }

    private List<OdfShape> FindShapesUsingMarker(string name)
    {
        var shapes = new List<OdfShape>();
        foreach (OdfNode node in ContentRoot.Descendants())
        {
            if (node.NodeType != OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Draw)
                continue;
            string? styleName = node.GetAttribute("style-name", OdfNamespaces.Draw);
            if (string.IsNullOrEmpty(styleName))
                continue;
            string? start = StyleEngine.GetStyleProperty(styleName!, "marker-start", OdfNamespaces.Draw, "graphic");
            string? end = StyleEngine.GetStyleProperty(styleName!, "marker-end", OdfNamespaces.Draw, "graphic");
            if (string.Equals(start, name, StringComparison.Ordinal) || string.Equals(end, name, StringComparison.Ordinal))
                shapes.Add(new OdfShape(node, this));
        }
        return shapes;
    }
}
