using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Draw Wrappers


/// <summary>
/// 表示 ODF 中的 draw:frame 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawFrameElement(string? prefix = null) : OdfElement("frame", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此框架的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:image 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawImageElement(string? prefix = null) : OdfElement("image", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定影像的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:object 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawObjectElement(string? prefix = null) : OdfElement("object", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定內嵌物件的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的繪圖形狀元素。
/// </summary>
/// <param name="shapeKind">形狀種類</param>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawShapeElement(string shapeKind, string? prefix = null) : OdfElement(shapeKind, OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此形狀的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:g 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawGroupElement(string? prefix = null) : OdfElement("g", OdfNamespaces.Draw, prefix);

/// <summary>
/// 表示 ODF 中的 draw:connector 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawConnectorElement(string? prefix = null) : OdfElement("connector", OdfNamespaces.Draw, prefix);


#endregion
