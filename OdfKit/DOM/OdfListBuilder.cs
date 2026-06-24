using System;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.DOM;

/// <summary>
/// 提供鏈式（Fluent）建構巢狀 ODF 清單結構（<c>text:list</c> 與 <c>text:list-item</c>）的建構器。
/// </summary>
public sealed class OdfListBuilder
{
    private readonly OdfNode _container;
    private readonly TextDocument _doc;
    private readonly OdfListBuilder? _parentBuilder;
    private readonly OdfNode _listNode;
    private OdfNode? _lastItemNode;

    internal OdfListBuilder(OdfNode container, TextDocument doc, OdfListBuilder? parentBuilder = null, string? styleName = null)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _parentBuilder = parentBuilder;

        _listNode = new OdfNode(OdfNodeType.Element, "list", OdfNamespaces.Text, "text");
        if (!string.IsNullOrEmpty(styleName))
        {
            _listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName!, "text");
        }
        _container.AppendChild(_listNode);
    }

    /// <summary>
    /// 在當前清單中新增一個清單項目，並寫入純文字內容。
    /// </summary>
    /// <param name="text">項目的純文字內容</param>
    /// <returns>當前清單建構器執行個體，支援鏈式呼叫</returns>
    public OdfListBuilder Item(string text)
    {
        _lastItemNode = new OdfNode(OdfNodeType.Element, "list-item", OdfNamespaces.Text, "text");
        _listNode.AppendChild(_lastItemNode);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        _lastItemNode.AppendChild(pNode);

        if (!string.IsNullOrEmpty(text))
        {
            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text };
            pNode.AppendChild(textNode);
        }

        return this;
    }

    /// <summary>
    /// 在當前清單中新增一個清單項目，並允許透過委派對該項目的段落（<see cref="OdfParagraph"/>）進行細部配置。
    /// </summary>
    /// <param name="configure">配置段落的委派動作</param>
    /// <returns>當前清單建構器執行個體，支援鏈式呼叫</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="configure"/> 為 <see langword="null"/> 時擲出</exception>
    public OdfListBuilder Item(Action<OdfParagraph> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        _lastItemNode = new OdfNode(OdfNodeType.Element, "list-item", OdfNamespaces.Text, "text");
        _listNode.AppendChild(_lastItemNode);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        _lastItemNode.AppendChild(pNode);

        var paragraph = new OdfParagraph(pNode, _doc);
        configure(paragraph);

        return this;
    }

    /// <summary>
    /// 在當前清單項目的下方，建立一個子（巢狀）清單。
    /// </summary>
    /// <param name="styleName">子清單的樣式名稱，選填</param>
    /// <returns>代表子清單的建構器執行個體</returns>
    public OdfListBuilder SubList(string? styleName = null)
    {
        if (_lastItemNode == null)
        {
            // 若目前沒有任何項目，自動補上一個空的 list-item 以維持結構合法性
            _lastItemNode = new OdfNode(OdfNodeType.Element, "list-item", OdfNamespaces.Text, "text");
            _listNode.AppendChild(_lastItemNode);
        }

        return new OdfListBuilder(_lastItemNode, _doc, this, styleName);
    }

    /// <summary>
    /// 回到上層清單建構器（若是頂層清單則回傳自身）。
    /// </summary>
    /// <returns>上層清單建構器執行個體</returns>
    public OdfListBuilder Up()
    {
        return _parentBuilder ?? this;
    }
}
