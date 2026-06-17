using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// 繪圖頁面圖層讀取引擎（內部協作者）。
/// </summary>
internal static class OdfDrawPageLayerReadEngine
{
    internal static IReadOnlyList<OdfLayerInfo> GetLayers(OdfDrawPage page) =>
        CollectLayers(page.Node, page.Name);

    private static List<OdfLayerInfo> CollectLayers(OdfNode pageNode, string pageName)
    {
        List<OdfLayerInfo> layers = [];

        foreach (OdfNode child in pageNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "layer-set" ||
                child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            foreach (OdfNode layerNode in child.Children)
            {
                if (layerNode.NodeType is not OdfNodeType.Element ||
                    layerNode.LocalName is not "layer" ||
                    layerNode.NamespaceUri != OdfNamespaces.Draw)
                    continue;

                string? name = layerNode.GetAttribute("name", OdfNamespaces.Draw);
                if (string.IsNullOrEmpty(name))
                    continue;

                bool isProtected = layerNode.GetAttribute("protected", OdfNamespaces.Draw) == "true";
                layers.Add(new OdfLayerInfo(
                    pageName,
                    name!,
                    isProtected,
                    layerNode.GetAttribute("display", OdfNamespaces.Draw)));
            }
        }

        return layers;
    }
}
