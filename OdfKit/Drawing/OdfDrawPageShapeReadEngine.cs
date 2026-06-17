using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// 繪圖頁面圖形讀取引擎（內部協作者）。
/// </summary>
internal static class OdfDrawPageShapeReadEngine
{
    internal static IReadOnlyList<OdfPathInfo> GetPaths(OdfDrawPage page) =>
        CollectPaths(page.Node, page.Name);

    internal static IReadOnlyList<OdfConnectorInfo> GetConnectors(OdfDrawPage page) =>
        CollectConnectors(page.Node, page.Name);

    internal static IReadOnlyList<OdfPolygonInfo> GetPolygons(OdfDrawPage page) =>
        CollectPolygons(page.Node, page.Name);

    internal static IReadOnlyList<OdfCustomShapeInfo> GetCustomShapes(OdfDrawPage page) =>
        CollectCustomShapes(page.Node, page.Name);

    internal static IReadOnlyList<OdfGroupInfo> GetGroups(OdfDrawPage page) =>
        CollectGroups(page.Node, page.Name);

    internal static IReadOnlyList<OdfDrawTextBoxInfo> GetTextBoxes(OdfDrawPage page) =>
        CollectTextBoxes(page.Node, page.Name);

    private static List<OdfPathInfo> CollectPaths(OdfNode parent, string pageName)
    {
        List<OdfPathInfo> paths = [];
        WalkDrawingNodes(parent, node =>
        {
            if (node.LocalName is not "path" || node.NamespaceUri != OdfNamespaces.Draw)
                return;

            string? pathData = node.GetAttribute("d", OdfNamespaces.Svg);
            if (string.IsNullOrEmpty(pathData))
                return;

            paths.Add(new OdfPathInfo(
                pageName,
                node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                pathData!,
                node.GetAttribute("x", OdfNamespaces.Svg),
                node.GetAttribute("y", OdfNamespaces.Svg),
                node.GetAttribute("width", OdfNamespaces.Svg),
                node.GetAttribute("height", OdfNamespaces.Svg)));
        });

        return paths;
    }

    private static List<OdfConnectorInfo> CollectConnectors(OdfNode parent, string pageName)
    {
        List<OdfConnectorInfo> connectors = [];
        WalkDrawingNodes(parent, node =>
        {
            if (node.LocalName is not "connector" || node.NamespaceUri != OdfNamespaces.Draw)
                return;

            connectors.Add(new OdfConnectorInfo(
                pageName,
                node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                ParseConnectorType(node.GetAttribute("type", OdfNamespaces.Draw)),
                node.GetAttribute("start-shape", OdfNamespaces.Draw),
                node.GetAttribute("end-shape", OdfNamespaces.Draw),
                node.GetAttribute("x1", OdfNamespaces.Svg),
                node.GetAttribute("y1", OdfNamespaces.Svg),
                node.GetAttribute("x2", OdfNamespaces.Svg),
                node.GetAttribute("y2", OdfNamespaces.Svg)));
        });

        return connectors;
    }

    private static List<OdfPolygonInfo> CollectPolygons(OdfNode parent, string pageName)
    {
        List<OdfPolygonInfo> polygons = [];
        WalkDrawingNodes(parent, node =>
        {
            if (node.LocalName is not "polygon" || node.NamespaceUri != OdfNamespaces.Draw)
                return;

            string? points = node.GetAttribute("points", OdfNamespaces.Draw);
            if (string.IsNullOrEmpty(points))
                return;

            polygons.Add(new OdfPolygonInfo(
                pageName,
                node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                points!,
                node.GetAttribute("x", OdfNamespaces.Svg),
                node.GetAttribute("y", OdfNamespaces.Svg),
                node.GetAttribute("width", OdfNamespaces.Svg),
                node.GetAttribute("height", OdfNamespaces.Svg)));
        });

        return polygons;
    }

    private static List<OdfCustomShapeInfo> CollectCustomShapes(OdfNode parent, string pageName)
    {
        List<OdfCustomShapeInfo> shapes = [];
        WalkDrawingNodes(parent, node =>
        {
            if (node.LocalName is not "custom-shape" || node.NamespaceUri != OdfNamespaces.Draw)
                return;

            string? geometryType = FindEnhancedGeometryType(node);
            if (string.IsNullOrEmpty(geometryType))
                return;

            shapes.Add(new OdfCustomShapeInfo(
                pageName,
                node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                geometryType!,
                node.GetAttribute("x", OdfNamespaces.Svg),
                node.GetAttribute("y", OdfNamespaces.Svg),
                node.GetAttribute("width", OdfNamespaces.Svg),
                node.GetAttribute("height", OdfNamespaces.Svg)));
        });

        return shapes;
    }

    private static List<OdfGroupInfo> CollectGroups(OdfNode parent, string pageName)
    {
        List<OdfGroupInfo> groups = [];
        CollectGroupsRecursive(parent, pageName, groups);
        return groups;
    }

    private static void CollectGroupsRecursive(OdfNode parent, string pageName, List<OdfGroupInfo> groups)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            if (child.LocalName is not "g")
                continue;

            groups.Add(new OdfGroupInfo(
                pageName,
                child.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                child.GetAttribute("name", OdfNamespaces.Draw)));

            CollectGroupsRecursive(child, pageName, groups);
        }
    }

    private static List<OdfDrawTextBoxInfo> CollectTextBoxes(OdfNode parent, string pageName)
    {
        List<OdfDrawTextBoxInfo> textBoxes = [];
        WalkDrawingNodes(parent, node =>
        {
            if (!ContainsDescendant(node, "text-box", OdfNamespaces.Draw))
                return;

            textBoxes.Add(new OdfDrawTextBoxInfo(
                pageName,
                node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                ExtractTextBoxContent(node),
                node.GetAttribute("x", OdfNamespaces.Svg),
                node.GetAttribute("y", OdfNamespaces.Svg),
                node.GetAttribute("width", OdfNamespaces.Svg),
                node.GetAttribute("height", OdfNamespaces.Svg)));
        });

        return textBoxes;
    }

    private static string ExtractTextBoxContent(OdfNode container)
    {
        foreach (OdfNode child in container.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName is "text-box" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                foreach (OdfNode paragraph in child.Children)
                {
                    if (paragraph.NodeType is OdfNodeType.Element &&
                        paragraph.LocalName is "p" &&
                        paragraph.NamespaceUri == OdfNamespaces.Text)
                        return paragraph.TextContent ?? string.Empty;
                }
            }

            if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
            {
                string nested = ExtractTextBoxContent(child);
                if (!string.IsNullOrEmpty(nested))
                    return nested;
            }
        }

        return string.Empty;
    }

    private static bool ContainsDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
                return true;

            if (child.NodeType is OdfNodeType.Element &&
                child.NamespaceUri == OdfNamespaces.Draw &&
                ContainsDescendant(child, localName, namespaceUri))
                return true;
        }

        return false;
    }

    private static void WalkDrawingNodes(OdfNode parent, System.Action<OdfNode> visit)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            if (child.LocalName == "g")
            {
                WalkDrawingNodes(child, visit);
                continue;
            }

            visit(child);
        }
    }

    private static string? FindEnhancedGeometryType(OdfNode customShapeNode)
    {
        foreach (OdfNode child in customShapeNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "enhanced-geometry" ||
                child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            return child.GetAttribute("type", OdfNamespaces.Draw);
        }

        return null;
    }

    private static OdfConnectorType ParseConnectorType(string? typeValue) => typeValue switch
    {
        "lines" => OdfConnectorType.Lines,
        "straight" => OdfConnectorType.Straight,
        "curve" => OdfConnectorType.Curve,
        _ => OdfConnectorType.Standard,
    };
}
