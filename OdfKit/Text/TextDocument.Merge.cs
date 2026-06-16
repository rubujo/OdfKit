using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Merging Logic Override


    /// <summary>
    /// 合併來源文件與目前文件的內容節點。
    /// </summary>
    /// <param name="sourceDoc">來源 OdfDocument 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">變更樣式名稱的映射字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcText = sourceDoc as TextDocument ?? throw new ArgumentException("Source document must be a TextDocument.");

        foreach (var child in srcText.BodyTextRoot.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcText.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                BodyTextRoot.AppendChild(imported);
            }
        }
    }


    #endregion
}
