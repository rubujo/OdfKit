using System.Xml;

namespace OdfKit.Core;

/// <summary>
/// 建立已預先載入常用 ODF 名稱的 XML 名稱表。
/// </summary>
internal static class OdfXmlNameTable
{
    private static readonly string[] Names =
    [
        "anim",
        "body",
        "boolean-value",
        "calcext",
        "chart",
        "config",
        "c",
        "creation-date",
        "creator",
        "date-value",
        "dc",
        "db",
        "document",
        "document-content",
        "document-meta",
        "document-settings",
        "document-styles",
        "draw",
        "dr3d",
        "ds",
        "dsig",
        "file-entry",
        "fo",
        "form",
        "formula",
        "full-path",
        "h",
        "href",
        "list-item",
        "manifest",
        "manifest:media-type",
        "math",
        "media-type",
        "meta",
        "mimetype",
        "name",
        "number",
        "number-columns-repeated",
        "number-columns-spanned",
        "number-rows-repeated",
        "number-rows-spanned",
        "office",
        "of",
        "oooc",
        "outline-level",
        "p",
        "presentation",
        "report",
        "script",
        "settings",
        "smil",
        "span",
        "spreadsheet",
        "style",
        "style-name",
        "svg",
        "table",
        "table-cell",
        "table-row",
        "text",
        "time-value",
        "value",
        "value-type",
        "version",
        "xlink",
        "xml",
        "xmlns"
    ];

    private static readonly string[] NamespaceUris =
    [
        OdfNamespaces.Office,
        OdfNamespaces.Style,
        OdfNamespaces.Text,
        OdfNamespaces.Table,
        OdfNamespaces.Draw,
        OdfNamespaces.Fo,
        OdfNamespaces.XLink,
        OdfNamespaces.Dc,
        OdfNamespaces.Meta,
        OdfNamespaces.Number,
        OdfNamespaces.Presentation,
        OdfNamespaces.Svg,
        OdfNamespaces.Chart,
        OdfNamespaces.Config,
        OdfNamespaces.Script,
        OdfNamespaces.Anim,
        OdfNamespaces.Smil,
        OdfNamespaces.Xml,
        OdfNamespaces.Manifest,
        OdfNamespaces.Dsig,
        OdfNamespaces.Ds,
        OdfNamespaces.Form,
        OdfNamespaces.CalcExt,
        OdfNamespaces.LoExt,
        OdfNamespaces.Pkg,
        OdfNamespaces.Of,
        OdfNamespaces.Oooc,
        OdfNamespaces.MathMl,
        OdfNamespaces.Dr3d,
        OdfNamespaces.Database,
        OdfNamespaces.Report,
        "http://www.w3.org/2000/xmlns/"
    ];

    internal static NameTable Create()
    {
        NameTable nameTable = new();
        Preload(nameTable);
        return nameTable;
    }

    internal static void Preload(XmlNameTable nameTable)
    {
        if (nameTable is null)
            throw new ArgumentNullException(nameof(nameTable));

        foreach (string name in Names)
        {
            nameTable.Add(name);
        }

        foreach (string namespaceUri in NamespaceUris)
        {
            nameTable.Add(namespaceUri);
            string prefix = OdfNamespaces.GetPrefix(namespaceUri);
            if (prefix.Length > 0)
            {
                nameTable.Add(prefix);
            }
        }
    }
}
