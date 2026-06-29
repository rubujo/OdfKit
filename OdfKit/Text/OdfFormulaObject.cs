using System;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Text;

/// <summary>
/// Represents odf formula object.
/// 表示 ODF 文件中的公式物件。
/// </summary>
/// <param name="frameNode">The value to use. / 公式的外框節點</param>
/// <param name="objectNode">The value to use. / 公式的物件節點</param>
/// <param name="doc">The value to use. / 所屬的文字文件</param>
public class OdfFormulaObject(OdfNode frameNode, OdfNode objectNode, TextDocument doc)
{
    /// <summary>
    /// Gets argument null exception.
    /// 取得公式的外框節點。
    /// </summary>
    public OdfNode FrameNode { get; } = frameNode ?? throw new ArgumentNullException(nameof(frameNode));

    /// <summary>
    /// Gets argument null exception.
    /// 取得公式的物件節點。
    /// </summary>
    public OdfNode ObjectNode { get; } = objectNode ?? throw new ArgumentNullException(nameof(objectNode));

    private readonly TextDocument _doc = doc ?? throw new ArgumentNullException(nameof(doc));

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定公式物件的名稱。
    /// </summary>
    public string? Name
    {
        get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
        set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定公式物件的錨定類型。
    /// </summary>
    public string? AnchorType
    {
        get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
        set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "as-char", "text");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定公式物件的寬度。
    /// </summary>
    public string? Width
    {
        get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定公式物件的高度。
    /// </summary>
    public string? Height
    {
        get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定公式在 ODF 封裝容器內的儲存資料夾路徑。
    /// </summary>
    public string? FormulaFolder
    {
        get => ObjectNode.GetAttribute("href", OdfNamespaces.XLink);
        set
        {
            if (value is not null)
            {
                // 目錄穿越防禦 (Zip Slip Defense)：在指定資料夾路徑時執行嚴格的路徑驗證
                if (value.Contains("..") || value.Contains("\\") || value.StartsWith("/"))
                {
                    throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaObject_InvalidFormulaFolderPath_2"));
                }
                ObjectNode.SetAttribute("href", OdfNamespaces.XLink, value, "xlink");
                ObjectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
                ObjectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
                ObjectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
            }
            else
            {
                ObjectNode.RemoveAttribute("href", OdfNamespaces.XLink);
                ObjectNode.RemoveAttribute("type", OdfNamespaces.XLink);
                ObjectNode.RemoveAttribute("show", OdfNamespaces.XLink);
                ObjectNode.RemoveAttribute("actuate", OdfNamespaces.XLink);
            }
        }
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定 MathML 的 XML 字串內容。
    /// </summary>
    public string MathMlXmlString
    {
        get
        {
            string? folder = FormulaFolder;
            if (folder is null || folder.Length == 0)
                return string.Empty;
            string contentPath = $"{folder.TrimEnd('/')}/content.xml";
            if (!_doc.Package.HasEntry(contentPath))
                return string.Empty;

            var bytes = _doc.Package.ReadEntry(contentPath);
            if (bytes is null)
                return string.Empty;

            string xml = Encoding.UTF8.GetString(bytes);
            return ExtractFormulaContent(xml);
        }
        set
        {
            string? folder = FormulaFolder;
            if (folder is null || folder.Length == 0)
            {
                folder = $"Formula_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                FormulaFolder = folder;
            }

            // 針對目錄穿越 (Zip Slip) 的防禦性檢查
            if (folder.Contains("..") || folder.Contains("\\") || folder.StartsWith("/"))
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaObject_InvalidFormulaFolderPath_2"));
            }

            string mathDocXml = $"<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:math=\"http://www.w3.org/1998/Math/MathML\" office:version=\"{OdfVersionInfo.DefaultVersionString}\"><office:body><office:formula>{value}</office:formula></office:body></office:document-content>";
            string stylesXml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"{OdfVersionInfo.DefaultVersionString}\"><office:styles/><office:automatic-styles/><office:master-styles/></office:document-styles>";
            string metaXml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"{OdfVersionInfo.DefaultVersionString}\"><office:meta/></office:document-meta>";

            string contentPath = $"{folder.TrimEnd('/')}/content.xml";
            string stylesPath = $"{folder.TrimEnd('/')}/styles.xml";
            string metaPath = $"{folder.TrimEnd('/')}/meta.xml";
            string mimePath = $"{folder.TrimEnd('/')}/mimetype";

            _doc.Package.WriteEntry(contentPath, Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
            _doc.Package.WriteEntry(stylesPath, Encoding.UTF8.GetBytes(stylesXml), "text/xml");
            _doc.Package.WriteEntry(metaPath, Encoding.UTF8.GetBytes(metaXml), "text/xml");
            _doc.Package.WriteEntry(mimePath, Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), string.Empty);
            _doc.Package.SaveManifestToEntries();
        }
    }

    private string ExtractFormulaContent(string documentXml)
    {
        if (string.IsNullOrWhiteSpace(documentXml))
            return string.Empty;

        int start = documentXml.IndexOf("<office:formula>");
        if (start == -1)
            start = documentXml.IndexOf("<office:formula ");
        if (start == -1)
            return string.Empty;

        int closeTagStart = documentXml.IndexOf('>', start);
        if (closeTagStart == -1)
            return string.Empty;

        int end = documentXml.IndexOf("</office:formula>", closeTagStart);
        if (end == -1)
            return string.Empty;

        return documentXml.Substring(closeTagStart + 1, end - closeTagStart - 1).Trim();
    }
}
