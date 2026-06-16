using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    #region Drawing Helpers

    private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");
        return frame;
    }

    private static OdfNode CreateLineLikeNode(string localName, OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var node = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
        node.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        node.SetAttribute("x1", OdfNamespaces.Svg, x1.ToString(), "svg");
        node.SetAttribute("y1", OdfNamespaces.Svg, y1.ToString(), "svg");
        node.SetAttribute("x2", OdfNamespaces.Svg, x2.ToString(), "svg");
        node.SetAttribute("y2", OdfNamespaces.Svg, y2.ToString(), "svg");
        return node;
    }

    private IReadOnlyList<T> FindDrawingObjects<T>(Func<OdfNode, bool> predicate, Func<OdfNode, T> factory)
    {
        List<T> objects = [];
        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && predicate(child))
            {
                objects.Add(factory(child));
            }
        }

        return objects.AsReadOnly();
    }

    private static bool ContainsDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return true;
            }

            if (ContainsDescendant(child, localName, namespaceUri))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
