using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Recording


    /// <summary>
    /// 取得或設定一個值，指出是否啟用修訂追蹤（追蹤修訂）。
    /// </summary>
    public bool TrackedChanges { get; set; }

    /// <summary>
    /// 記錄修訂追蹤資訊。
    /// </summary>
    /// <param name="changeType">修訂類型</param>
    /// <param name="extraContent">修訂的附加內容節點</param>
    /// <param name="originalStyleName">原本的樣式名稱</param>
    /// <param name="targetFamily">目標樣式系列名稱</param>
    /// <returns>產生的修訂識別碼</returns>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null)
    {
        return AddTrackedChange(changeType, "Author", DateTime.UtcNow, extraContent, originalStyleName, targetFamily);
    }

    /// <summary>
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    /// <param name="changeType">修訂類型（"insertion"、"deletion" 或 "format-change"）。</param>
    /// <param name="creator">建立者姓名。</param>
    /// <param name="date">修訂時間。</param>
    /// <param name="extraContent">修訂的附加內容節點。</param>
    /// <param name="originalStyleName">原本的樣式名稱。</param>
    /// <param name="targetFamily">目標樣式系列名稱。</param>
    /// <returns>產生的修訂識別碼。</returns>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null)
    {
        OdfNode? tcNode = null;
        foreach (var child in BodyTextRoot.Children)
        {
            if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Text)
            {
                tcNode = child;
                break;
            }
        }
        if (tcNode is null)
        {
            tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            if (BodyTextRoot.Children.Count > 0)
                BodyTextRoot.InsertBefore(tcNode, BodyTextRoot.Children[0]);
            else
                BodyTextRoot.AppendChild(tcNode);
        }

        string changeId = "ct_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
        changedRegion.SetAttribute("id", OdfNamespaces.Text, changeId, "text");

        var typeNode = new OdfNode(OdfNodeType.Element, changeType, OdfNamespaces.Text, "text");
        if (changeType == "deletion" && extraContent is not null)
        {
            typeNode.AppendChild(extraContent.CloneNode(true));
        }
        else if (changeType == "format-change")
        {
            if (originalStyleName is not null)
            {
                typeNode.SetAttribute("style-name", OdfNamespaces.Text, originalStyleName, "text");
            }
            if (targetFamily is not null)
            {
                typeNode.SetAttribute("target-family", OdfNamespaces.Text, targetFamily, "text");
            }
        }
        changedRegion.AppendChild(typeNode);

        var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");
        typeNode.AppendChild(changeInfo);

        var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creatorNode.TextContent = creator;
        changeInfo.AppendChild(creatorNode);

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.TextContent = date == DateTime.MinValue ? "0001-01-01T00:00:00Z" :
                               date == DateTime.MaxValue ? "9999-12-31T23:59:59.9999999Z" :
                               date.ToString("yyyy-MM-ddTHH:mm:ssZ");
        changeInfo.AppendChild(dateNode);

        tcNode.AppendChild(changedRegion);
        return changeId;
    }

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges()
    {
        AcceptAllTrackedChanges();
    }

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllChanges()
    {
        RejectAllTrackedChanges();
    }

    /// <summary>
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    /// <returns>追蹤修訂的集合。</returns>
    public IEnumerable<OdfTrackedChange> GetTrackedChanges()
    {
        var list = new List<OdfTrackedChange>();
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return list;

        foreach (var changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id))
                continue;

            string changeType = "";
            string creator = "";
            DateTime date = DateTime.MinValue;
            OdfNode? specNode = null;

            foreach (var child in changedRegion.Children)
            {
                if ((child.LocalName == "insertion" || child.LocalName == "deletion" || child.LocalName == "format-change") &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    changeType = child.LocalName;
                    specNode = child;
                    break;
                }
            }

            if (specNode is not null)
            {
                var changeInfo = FindChild(specNode, "change-info", OdfNamespaces.Office);
                if (changeInfo is not null)
                {
                    var creatorNode = FindChild(changeInfo, "creator", OdfNamespaces.Dc);
                    if (creatorNode is not null)
                        creator = creatorNode.TextContent ?? "";

                    var dateNode = FindChild(changeInfo, "date", OdfNamespaces.Dc);
                    if (dateNode is not null)
                    {
                        var textContent = dateNode.TextContent;
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            if (textContent == "0001-01-01T00:00:00Z" || textContent.StartsWith("0001-01-01"))
                            {
                                date = DateTime.MinValue;
                            }
                            else if (textContent == "9999-12-31T23:59:59.9999999Z" || textContent.StartsWith("9999-12-31"))
                            {
                                date = DateTime.MaxValue;
                            }
                            else if (DateTime.TryParse(textContent, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate))
                            {
                                date = parsedDate;
                            }
                        }
                    }
                }
            }

            string content = "";
            if (changeType == "deletion" && specNode is not null)
            {
                var sb = new System.Text.StringBuilder();
                ExtractTextContentIgnoringChangeInfo(specNode, sb);
                content = sb.ToString();
            }
            else if (changeType == "insertion" || changeType == "format-change")
            {
                content = ExtractTextBetweenMarkers(BodyTextRoot, id!);
            }

            list.Add(new OdfTrackedChange
            {
                RegionId = id!,
                ChangeType = changeType switch
                {
                    "deletion" => OdfChangeType.Deletion,
                    "format-change" => OdfChangeType.FormatChange,
                    _ => OdfChangeType.Insertion,
                },
                Author = creator,
                ChangedAt = date,
                Content = content,
            });
        }

        return list;
    }
    /// <summary>
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">發生變更的 ODF 節點</param>
    /// <param name="family">樣式系列名稱</param>
    public void TrackFormatChange(OdfNode node, string family)
    {
        if (!TrackedChanges)
            return;

        string styleAttr = "style-name";
        string styleNs = family switch
        {
            "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
            "graphic" => OdfNamespaces.Draw,
            _ => OdfNamespaces.Text
        };
        string? originalStyleName = node.GetAttribute(styleAttr, styleNs);

        string changeId = RecordTrackedChange("format-change", null, originalStyleName, family);

        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

        if (node.LocalName == "p" || node.LocalName == "h")
        {
            if (node.Children.Count > 0)
            {
                node.InsertBefore(startNode, node.Children[0]);
            }
            else
            {
                node.AppendChild(startNode);
            }
            node.AppendChild(endNode);
        }
        else
        {
            var parent = node.Parent;
            if (parent is not null)
            {
                parent.InsertBefore(startNode, node);
                parent.InsertAfter(endNode, node);
            }
        }
    }

    /// <summary>
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    /// <param name="node">要刪除的 ODF 節點</param>
    public void DeleteNode(OdfNode node)
    {
        if (node.Parent is null)
            return;
        var parent = node.Parent;

        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("deletion", node);

            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            parent.InsertBefore(startNode, node);
            parent.InsertAfter(endNode, node);
            parent.RemoveChild(node);
        }
        else
        {
            parent.RemoveChild(node);
        }
    }

    #endregion
}
