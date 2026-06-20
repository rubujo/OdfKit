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

    /// <summary>Form 命名空間 URI</summary>
    public const string Form = "urn:oasis:names:tc:opendocument:xmlns:form:1.0";

    /// <summary>CalcExt 命名空間 URI（LibreOffice 電子試算表計算擴充命名空間）</summary>
    public const string CalcExt = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";

    /// <summary>LoExt 命名空間 URI（LibreOffice 實驗性 Office 擴充命名空間）</summary>
    public const string LoExt = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";

    /// <summary>封裝 RDF metadata 的 Pkg 命名空間 URI（ODF 1.2 meta/pkg ontology）</summary>
    public const string Pkg = OdfPkgRdfPredicates.NamespaceUri;

    /// <summary>OpenFormula 命名空間 URI（ODF 1.2+ 公式語言，<c>table:formula</c> 等屬性值內 <c>of:=</c> 前綴所指涉的命名空間）</summary>
    public const string Of = "urn:oasis:names:tc:opendocument:xmlns:of:1.2";

    /// <summary>OpenOffice.org Calc 相容命名空間 URI（<c>table:condition</c> 內容驗證條件值內 <c>oooc:</c> 前綴所指涉的命名空間）</summary>
    public const string Oooc = "http://openoffice.org/2004/calc";

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
            CalcExt => "calcext",
            LoExt => "loext",
            Form => "form",
            Pkg => "pkg",
            Of => "of",
            Oooc => "oooc",
            _ => string.Empty
        };
    }
}

