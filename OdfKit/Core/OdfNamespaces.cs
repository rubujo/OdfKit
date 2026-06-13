namespace OdfKit.Core;

/// <summary>
/// 提供常用 ODF XML 命名空間 URI 的常數與公用程式方法。
/// </summary>
public static class OdfNamespaces
{
    /// <summary>Office 命名空間 URI</summary>
    public const string Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    /// <summary>Style 命名空間 URI</summary>
    public const string Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    /// <summary>Text 命名空間 URI</summary>
    public const string Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    /// <summary>Table 命名空間 URI</summary>
    public const string Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    /// <summary>Draw 命名空間 URI</summary>
    public const string Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    /// <summary>Fo 命名空間 URI</summary>
    public const string Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    /// <summary>XLink 命名空間 URI</summary>
    public const string XLink = "http://www.w3.org/1999/xlink";

    /// <summary>Dc 命名空間 URI</summary>
    public const string Dc = "http://purl.org/dc/elements/1.1/";

    /// <summary>Meta 命名空間 URI</summary>
    public const string Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

    /// <summary>Number 命名空間 URI</summary>
    public const string Number = "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0";

    /// <summary>Presentation 命名空間 URI</summary>
    public const string Presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";

    /// <summary>Svg 命名空間 URI</summary>
    public const string Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

    /// <summary>Chart 命名空間 URI</summary>
    public const string Chart = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";

    /// <summary>Config 命名空間 URI</summary>
    public const string Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    /// <summary>Script 命名空間 URI</summary>
    public const string Script = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";

    /// <summary>Manifest 命名空間 URI</summary>
    public const string Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    /// <summary>Dsig 命名空間 URI</summary>
    public const string Dsig = "urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0";

    /// <summary>Ds 命名空間 URI</summary>
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>
    /// 取得指定命名空間 URI 的標準前綴。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <returns>對應的前綴；若無對應則傳回空字串</returns>
    public static string GetPrefix(string namespaceUri)
    {
        return namespaceUri switch
        {
            Office => "office",
            Style => "style",
            Text => "text",
            Table => "table",
            Draw => "draw",
            Fo => "fo",
            XLink => "xlink",
            Dc => "dc",
            Meta => "meta",
            Number => "number",
            Presentation => "presentation",
            Svg => "svg",
            Chart => "chart",
            Config => "config",
            Script => "script",
            Manifest => "manifest",
            Dsig => "dsig",
            Ds => "ds",
            _ => string.Empty
        };
    }
}

