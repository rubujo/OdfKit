using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;

using OdfKit.Compliance;
namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    /// <summary>
    /// 將指定圖形合併為新群組。
    /// </summary>
    /// <param name="shapeIds">要群組的圖形識別碼集合</param>
    /// <param name="name">選用的群組名稱</param>
    /// <returns>新建立的群組執行個體</returns>
    public OdfDrawGroup GroupShapes(IEnumerable<string> shapeIds, string? name = null)
    {
        if (shapeIds is null)
            throw new ArgumentNullException(nameof(shapeIds));

        List<string> ids = shapeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawPage_LeastOneGraphicIdentifier"), nameof(shapeIds));

        OdfDrawGroup group = AddGroup(name);
        foreach (string id in ids)
        {
            OdfNode shapeNode = FindShapeNodeById(id)
                ?? throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfDrawPage_NoGraphicFoundIdentifier", id));

            shapeNode.Parent?.RemoveChild(shapeNode);
            group.Node.AppendChild(shapeNode);
        }

        return group;
    }

    private OdfNode? FindShapeNodeById(string shapeId)
    {
        return FindShapeNodeById(Node, shapeId);
    }

    private static OdfNode? FindShapeNodeById(OdfNode parent, string shapeId)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            if (child.GetAttribute("id", OdfNamespaces.Draw) == shapeId)
                return child;

            OdfNode? nested = FindShapeNodeById(child, shapeId);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
