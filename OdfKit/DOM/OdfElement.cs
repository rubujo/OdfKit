using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 所有專門類型 ODF 元素包裝器的基底類別。
/// </summary>
/// <param name="localName">元素局部名稱</param>
/// <param name="namespaceUri">元素命名空間 URI</param>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OdfElement(string localName, string namespaceUri, string? prefix = null)
    : OdfNode(OdfNodeType.Element, localName, namespaceUri, prefix)
{
    /// <summary>
    /// 取得具有版本內容且結構定義說明的屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttributeValue(string localName, string namespaceUri, OdfVersion version = OdfVersion.Odf14)
    {
        var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
        if (attrDef is null)
        {
            OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
        }
        return GetAttribute(localName, namespaceUri);
    }

    /// <summary>
    /// 設定具有版本內容且結構定義說明的屬性值。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetAttributeValue(string localName, string namespaceUri, string value, string? prefix = null, OdfVersion version = OdfVersion.Odf14)
    {
        var attrDef = OdfSchemaRegistry.GetSchema(version).FindAttribute(namespaceUri, localName);
        if (attrDef is null)
        {
            OdfKitDiagnostics.Warn($"Attribute '{localName}' in namespace '{namespaceUri}' is not defined in ODF {version} schema.");
        }
        SetAttribute(localName, namespaceUri, value, prefix);
    }

    /// <summary>
    /// 列舉此元素的直接子元素，並只傳回指定的 typed DOM 元素型別。
    /// </summary>
    /// <typeparam name="TElement">要篩選的 typed DOM 元素型別</typeparam>
    /// <returns>符合型別的直接子元素列舉</returns>
    public IEnumerable<TElement> ChildElements<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode child in Children)
        {
            if (child is TElement typedChild)
            {
                yield return typedChild;
            }
        }
    }

    /// <summary>
    /// 取得第一個符合指定 typed DOM 型別的直接子元素。
    /// </summary>
    /// <typeparam name="TElement">要尋找的 typed DOM 元素型別</typeparam>
    /// <returns>第一個符合型別的直接子元素；找不到時為 <see langword="null"/></returns>
    public TElement? FirstChildElement<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode child in Children)
        {
            if (child is TElement typedChild)
            {
                return typedChild;
            }
        }

        return null;
    }

    /// <summary>
    /// 列舉此元素的所有後代元素，並只傳回指定的 typed DOM 元素型別。
    /// </summary>
    /// <typeparam name="TElement">要篩選的 typed DOM 元素型別</typeparam>
    /// <returns>符合型別的後代元素列舉</returns>
    public IEnumerable<TElement> DescendantElements<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode descendant in Descendants())
        {
            if (descendant is TElement typedDescendant)
            {
                yield return typedDescendant;
            }
        }
    }

    /// <summary>
    /// 取得第一個符合指定 typed DOM 型別的後代元素。
    /// </summary>
    /// <typeparam name="TElement">要尋找的 typed DOM 元素型別</typeparam>
    /// <returns>第一個符合型別的後代元素；找不到時為 <see langword="null"/></returns>
    public TElement? FirstDescendantElement<TElement>()
        where TElement : OdfElement
    {
        foreach (OdfNode descendant in Descendants())
        {
            if (descendant is TElement typedDescendant)
            {
                return typedDescendant;
            }
        }

        return null;
    }

    /// <summary>
    /// 將 typed DOM 元素加入此元素的子節點清單末尾，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要加入的 typed DOM 元素型別</typeparam>
    /// <param name="child">要加入的 typed DOM 子元素</param>
    /// <returns>已加入的 typed DOM 子元素</returns>
    public TElement AppendElement<TElement>(TElement child)
        where TElement : OdfElement
    {
        AppendChild(child);
        return child;
    }

    /// <summary>
    /// 在參考子節點之前插入 typed DOM 元素，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要插入的 typed DOM 元素型別</typeparam>
    /// <param name="newChild">要插入的 typed DOM 子元素</param>
    /// <param name="refChild">參考子節點，新元素將插入在此節點之前</param>
    /// <returns>已插入的 typed DOM 子元素</returns>
    public TElement InsertElementBefore<TElement>(TElement newChild, OdfNode refChild)
        where TElement : OdfElement
    {
        InsertBefore(newChild, refChild);
        return newChild;
    }

    /// <summary>
    /// 在參考子節點之後插入 typed DOM 元素，並傳回同一個元素以便串接設定。
    /// </summary>
    /// <typeparam name="TElement">要插入的 typed DOM 元素型別</typeparam>
    /// <param name="newChild">要插入的 typed DOM 子元素</param>
    /// <param name="refChild">參考子節點，新元素將插入在此節點之後</param>
    /// <returns>已插入的 typed DOM 子元素</returns>
    public TElement InsertElementAfter<TElement>(TElement newChild, OdfNode refChild)
        where TElement : OdfElement
    {
        InsertAfter(newChild, refChild);
        return newChild;
    }

    #region Clone

    /// <summary>
    /// 複製目前元素，傳回新的類型元素執行個體。
    /// </summary>
    /// <param name="deep">是否進行深層複製（遞迴複製子節點）</param>
    /// <returns>複製的新元素</returns>
    public override OdfNode CloneNode(bool deep)
    {
        var clone = OdfNodeFactory.CreateElement(LocalName, NamespaceUri, Prefix);
        foreach (var attr in Attributes)
        {
            clone.Attributes[attr.Key] = attr.Value;

            // 必須一併複製屬性的原始命名空間前綴；否則在序列化時，對於未登錄於
            // OdfNamespaces 標準前綴對映表的命名空間（例如保留的第三方擴充屬性），
            // 將因前綴資訊遺失而被改寫成自動產生的 "nsN" 佔位前綴。
            string? attrPrefix = GetAttributePrefix(attr.Key);
            if (attrPrefix is not null)
            {
                clone.SetAttribute(attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value, attrPrefix);
            }
        }
        if (deep && TryCopyLazyXmlStateTo(clone))
        {
            return clone;
        }

        if (deep)
        {
            foreach (var child in Children)
            {
                clone.AppendChild(child.CloneNode(true));
            }
        }
        return clone;
    }

    #endregion

    #region ComputedStyle Cache

    private OdfKit.Core.ComputedStyle? _computedStyle;
    private bool _isStyleDirty = true;

    /// <summary>
    /// 取得此元素經過層疊繼承解析後的實質最終樣式。
    /// </summary>
    public OdfKit.Core.ComputedStyle ComputedStyle
    {
        get
        {
            if (_isStyleDirty || _computedStyle == null)
            {
                _computedStyle = OdfKit.Core.ComputedStyle.Resolve(this);
                _isStyleDirty = false;
            }
            return _computedStyle;
        }
    }

    /// <summary>
    /// 使此元素及其所有子元素的層疊樣式快取失效。
    /// </summary>
    public override void InvalidateStyle()
    {
        if (!_isStyleDirty)
        {
            _isStyleDirty = true;
            foreach (var child in Children)
            {
                child.InvalidateStyle();
            }
        }
    }

    #endregion

}
