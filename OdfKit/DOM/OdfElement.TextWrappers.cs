using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Text Wrappers


/// <summary>
/// 表示 ODF 中的 text:p 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextPElement(string? prefix = null) : OdfElement("p", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此段落的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:h 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextHElement(string? prefix = null) : OdfElement("h", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此標題的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此標題的大綱層級。
    /// </summary>
    public int OutlineLevel
    {
        get => GetInt32AttributeValue("outline-level", OdfNamespaces.Text, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("outline-level", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
    }
}

/// <summary>
/// 表示 ODF 中的 text:span 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextSpanElement(string? prefix = null) : OdfElement("span", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此文字區段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:list 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextListElement(string? prefix = null) : OdfElement("list", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此清單的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:list-item 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextListItemElement(string? prefix = null) : OdfElement("list-item", OdfNamespaces.Text, prefix);

/// <summary>
/// 表示 ODF 中的 text:section 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextSectionElement(string? prefix = null) : OdfElement("section", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此區段的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Text);
            else
                SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此區段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Text);
            else
                SetAttributeValue("style-name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:bookmark 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextBookmarkElement(string? prefix = null) : OdfElement("bookmark", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此書籤的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Text);
            else
                SetAttributeValue("name", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 text:note 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TextNoteElement(string? prefix = null) : OdfElement("note", OdfNamespaces.Text, prefix)
{
    /// <summary>
    /// 取得或設定此註腳或章節附註的類別。
    /// </summary>
    public string? NoteClass
    {
        get => GetAttributeValue("note-class", OdfNamespaces.Text, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("note-class", OdfNamespaces.Text);
            else
                SetAttributeValue("note-class", OdfNamespaces.Text, value, OdfNamespaces.GetPrefix(OdfNamespaces.Text), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 office:annotation 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeAnnotationElement(string? prefix = null) : OdfElement("annotation", OdfNamespaces.Office, prefix)
{
    /// <summary>
    /// 取得或設定此註解的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Office, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Office);
            else
                SetAttributeValue("name", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
        }
    }
}


#endregion
