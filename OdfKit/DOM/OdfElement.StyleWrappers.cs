using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Style Wrappers


/// <summary>
/// 表示 ODF 中的 style:style 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleStyleElement(string? prefix = null) : OdfElement("style", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此樣式的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此樣式的家族類型。
    /// </summary>
    public string? Family
    {
        get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("family", OdfNamespaces.Style);
            else
                SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:default-style 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleDefaultStyleElement(string? prefix = null) : OdfElement("default-style", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此預設樣式的家族類型。
    /// </summary>
    public string? Family
    {
        get => GetAttributeValue("family", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("family", OdfNamespaces.Style);
            else
                SetAttributeValue("family", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:master-page 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleMasterPageElement(string? prefix = null) : OdfElement("master-page", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此母片頁面的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:page-layout 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StylePageLayoutElement(string? prefix = null) : OdfElement("page-layout", OdfNamespaces.Style, prefix)
{
    /// <summary>
    /// 取得或設定此頁面版面配置的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Style, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Style);
            else
                SetAttributeValue("name", OdfNamespaces.Style, value, OdfNamespaces.GetPrefix(OdfNamespaces.Style), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 style:text-properties 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleTextPropertiesElement(string? prefix = null) : OdfElement("text-properties", OdfNamespaces.Style, prefix);

/// <summary>
/// 表示 ODF 中的 style:paragraph-properties 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class StyleParagraphPropertiesElement(string? prefix = null) : OdfElement("paragraph-properties", OdfNamespaces.Style, prefix);


#endregion
