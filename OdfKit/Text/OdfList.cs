using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中的清單。
/// </summary>
public class OdfList
{
    internal OdfList(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此清單相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

    /// <summary>
    /// 取得或設定此清單的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set => Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定一個值，指出清單編號是否延續上一清單。
    /// </summary>
    public bool? ContinueNumbering
    {
        get => Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "true" ? true : (Node.GetAttribute("continue-numbering", OdfNamespaces.Text) == "false" ? false : null);
        set
        {
            if (value.HasValue)
                Node.SetAttribute("continue-numbering", OdfNamespaces.Text, value.Value ? "true" : "false", "text");
            else
                Node.RemoveAttribute("continue-numbering", OdfNamespaces.Text);
        }
    }

    /// <summary>
    /// 在清單中新增清單項目。
    /// </summary>
    /// <param name="text">項目預設段落文字內容</param>
    /// <returns>新建立的清單項目執行個體</returns>
    public OdfListItem AddListItem(string text = "")
    {
        var itemNode = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
        Node.AppendChild(itemNode);
        var item = new OdfListItem(itemNode, _doc);
        if (!string.IsNullOrEmpty(text))
        {
            item.AddParagraph(text);
        }
        return item;
    }

    /// <summary>
    /// 在指定層級新增清單項目（1-based）。層級 1 直接加入此清單；
    /// 層級 2 以上則自動建立/沿用巢狀清單結構。
    /// </summary>
    /// <param name="text">項目文字內容。</param>
    /// <param name="level">目標層級，從 1 開始，最大值為 10。</param>
    /// <returns>新建立的清單項目。</returns>
    public OdfListItem AddItem(string text, int level = 1)
    {
        if (level < 1)
            level = 1;
        if (level > 10)
            level = 10;
        if (level == 1)
            return AddListItem(text);

        OdfNode currentList = Node;
        for (int l = 1; l < level; l++)
        {
            var lastItem = FindLastListItem(currentList);
            if (lastItem is null)
            {
                var parentItem = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
                currentList.AppendChild(parentItem);
                lastItem = parentItem;
            }
            OdfNode? nestedList = FindNestedList(lastItem);
            if (nestedList is null)
            {
                nestedList = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
                if (!string.IsNullOrEmpty(StyleName))
                    nestedList.SetAttribute("style-name", OdfNamespaces.Text, StyleName!, "text");
                lastItem.AppendChild(nestedList);
            }
            currentList = nestedList;
        }

        var itemNode = OdfNodeFactory.CreateElement("list-item", OdfNamespaces.Text, "text");
        currentList.AppendChild(itemNode);
        var item = new OdfListItem(itemNode, _doc);
        item.AddParagraph(text);
        return item;
    }

    private static OdfNode? FindLastListItem(OdfNode listNode)
    {
        OdfNode? last = null;
        foreach (var child in listNode.Children)
        {
            if (child.LocalName == "list-item" && child.NamespaceUri == OdfNamespaces.Text)
                last = child;
        }
        return last;
    }

    private static OdfNode? FindNestedList(OdfNode itemNode)
    {
        foreach (var child in itemNode.Children)
        {
            if (child.LocalName == "list" && child.NamespaceUri == OdfNamespaces.Text)
                return child;
        }
        return null;
    }

    /// <summary>
    /// 重新開始清單的編號。
    /// </summary>
    /// <param name="startValue">開始數值</param>
    public void RestartNumbering(int startValue = 1)
    {
        ContinueNumbering = false;
        var firstItemNode = Node.Children.FirstOrDefault(c => c.LocalName == "list-item" && c.NamespaceUri == OdfNamespaces.Text);
        if (firstItemNode is not null)
        {
            var item = new OdfListItem(firstItemNode, _doc);
            item.StartValue = startValue;
        }
    }

    /// <summary>
    /// 設定清單的起始編號。
    /// </summary>
    /// <param name="value">起始編號；ODF 1.4 允許從 0 開始。</param>
    /// <returns>目前清單執行個體。</returns>
    public OdfList StartFrom(int value)
    {
        RestartNumbering(value);
        return this;
    }

    /// <summary>
    /// 取得清單項目清單。
    /// </summary>
    public IReadOnlyList<OdfListItem> Items
    {
        get
        {
            List<OdfListItem> items = [];
            foreach (OdfNode child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "list-item" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    items.Add(new OdfListItem(child, _doc));
                }
            }

            return items.AsReadOnly();
        }
    }
}
