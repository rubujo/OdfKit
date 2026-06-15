#pragma warning restore CS1591

using System;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit.Core;

/// <summary>
/// 建立最小的低階 ODF 封裝與扁平 XML 文件。
/// </summary>
public static class OdfDocumentFactory
{
    /// <summary>
    /// 建立指定種類的高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="kind">要建立的 ODF 文件種類。</param>
    /// <returns>建立完成的 ODF 文件。</returns>
    public static OdfDocument CreateDocument(OdfDocumentKind kind)
    {
        OdfDocumentKind packageKind = OdfDocumentKindDetector.IsFlatKind(kind)
            ? OdfDocumentKindDetector.ToContentKind(kind)
            : kind;

        var stream = new MemoryStream();
        OdfPackage package = OdfPackage.Create(stream);
        InitializeMinimalPackage(package, packageKind);
        package.IsFlatXml = OdfDocumentKindDetector.IsFlatKind(kind);
        return CreateDocumentWrapper(package, kind);
    }

    /// <summary>
    /// 從指定路徑載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="path">ODF 文件路徑。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(string path)
    {
        return LoadDocument(path, options: null);
    }

    /// <summary>
    /// 從指定路徑與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="path">ODF 文件路徑。</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(string path, OdfLoadOptions? options)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        OdfPackage package = OdfPackage.Open(path, options);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, path));
    }

    /// <summary>
    /// 從指定資料流載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(Stream stream, string? fileName = null)
    {
        return LoadDocument(stream, options: null, fileName);
    }

    /// <summary>
    /// 從指定資料流與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流。</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(Stream stream, OdfLoadOptions? options, string? fileName = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        OdfPackage package = OdfPackage.Open(stream, options: options);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, fileName));
    }

    /// <summary>
    /// 在提供的資料流中建立一個最小封裝的 ODF 文件。
    /// </summary>
    /// <param name="stream">接收封裝 ODF 文件的資料流</param>
    /// <param name="kind">ODF 文件的類型</param>
    /// <param name="version">ODF 規格版本</param>
    /// <param name="leaveOpen">若為 <see langword="true"/> ，則在釋放封裝後保持資料流開啟；否則為 <see langword="false"/></param>
    /// <param name="options">儲存文件的選項</param>
    /// <returns>傳回建立的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage CreatePackage(
        Stream stream,
        OdfDocumentKind kind,
        OdfVersion version = OdfVersion.Odf14,
        bool leaveOpen = false,
        OdfSaveOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("扁平 ODF 類型必須使用 WriteFlatXml 建立。", nameof(kind));

        var package = OdfPackage.Create(stream, leaveOpen, options);
        InitializeMinimalPackage(package, kind, version);
        return package;
    }

    /// <summary>
    /// 在提供的路徑上建立一個最小封裝的 ODF 文件。
    /// </summary>
    /// <param name="path">建立封裝 ODF 文件的檔案路徑</param>
    /// <param name="kind">ODF 文件的類型</param>
    /// <param name="version">ODF 規格版本</param>
    /// <param name="options">儲存文件的選項</param>
    /// <returns>傳回建立的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage CreatePackage(
        string path,
        OdfDocumentKind kind,
        OdfVersion version = OdfVersion.Odf14,
        OdfSaveOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("扁平 ODF 類型必須使用 WriteFlatXml 建立。", nameof(kind));

        var package = OdfPackage.Create(path, options);
        InitializeMinimalPackage(package, kind, version);
        return package;
    }

    /// <summary>
    /// 將最小的扁平 XML ODF 文件寫入至提供的資料流中。
    /// </summary>
    /// <param name="stream">寫入扁平 XML ODF 文件的資料流</param>
    /// <param name="kind">ODF 文件的類型</param>
    /// <param name="version">ODF 規格版本</param>
    /// <param name="leaveOpen">若為 <see langword="true"/> ，則保持資料流開啟；否則為 <see langword="false"/></param>
    public static void WriteFlatXml(
        Stream stream,
        OdfDocumentKind kind,
        OdfVersion version = OdfVersion.Odf14,
        bool leaveOpen = true)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        OdfDocumentKind flatKind = OdfDocumentKindDetector.ToFlatKind(kind);
        if (!OdfDocumentKindDetector.IsFlatKind(flatKind))
        {
            throw new ArgumentException("所提供的文件類型不具備扁平 XML ODF 格式。", nameof(kind));
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = !leaveOpen
        };

        using XmlWriter writer = XmlWriter.Create(stream, settings);
        string mimeType = GetMimeType(GetPackagedKind(flatKind));
        string versionText = FormatVersion(version);
        string bodyElement = GetBodyElementName(flatKind);

        writer.WriteStartDocument();
        writer.WriteStartElement("office", "document", OdfNamespaces.Office);
        WriteCommonNamespaces(writer);
        writer.WriteAttributeString("office", "mimetype", OdfNamespaces.Office, mimeType);
        writer.WriteAttributeString("office", "version", OdfNamespaces.Office, versionText);
        writer.WriteStartElement("office", "body", OdfNamespaces.Office);
        writer.WriteStartElement("office", bodyElement, OdfNamespaces.Office);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    /// <summary>
    /// 以指定的文件類型在封裝中填入最小的 ODF 實體。
    /// </summary>
    /// <param name="package">要初始化的 OdfPackage 執行個體</param>
    /// <param name="kind">ODF 文件的類型</param>
    /// <param name="version">ODF 規格版本</param>
    public static void InitializeMinimalPackage(
        OdfPackage package,
        OdfDocumentKind kind,
        OdfVersion version = OdfVersion.Odf14)
    {
        if (package is null) throw new ArgumentNullException(nameof(package));
        if (OdfDocumentKindDetector.IsFlatKind(kind)) throw new ArgumentException("扁平 ODF 類型無法儲存為封裝 ODF 類型。", nameof(kind));

        string mimeType = GetMimeType(kind);
        string versionText = FormatVersion(version);

        package.Version = version;
        package.SetMimeType(mimeType);
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(CreateContentXml(kind, versionText)), "text/xml");
        package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(CreateStylesXml(versionText, kind)), "text/xml");
        package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes(CreateMetaXml(versionText)), "text/xml");
        package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes(CreateSettingsXml(versionText)), "text/xml");
    }

    private static string CreateContentXml(OdfDocumentKind kind, string version)
    {
        string bodyElement = GetBodyElementName(kind);
        return "<office:document-content" +
            CommonNamespaceAttributes +
            " office:version=\"" + version + "\"><office:body><office:" +
            bodyElement +
            " /></office:body></office:document-content>";
    }

    private static string CreateStylesXml(string version, OdfDocumentKind kind)
    {
        string masterStyles = IsTextDocumentKind(kind)
            ? "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles>"
            : "<office:master-styles />";
        return "<office:document-styles" +
            CommonNamespaceAttributes +
            " office:version=\"" + version + "\"><office:styles /><office:automatic-styles />" + masterStyles + "</office:document-styles>";
    }

    private static bool IsTextDocumentKind(OdfDocumentKind kind) =>
        kind == OdfDocumentKind.Text ||
        kind == OdfDocumentKind.TextTemplate ||
        kind == OdfDocumentKind.TextMaster;

    private static string CreateMetaXml(string version)
    {
        return "<office:document-meta" +
            CommonNamespaceAttributes +
            " office:version=\"" + version + "\"><office:meta /></office:document-meta>";
    }

    private static string CreateSettingsXml(string version)
    {
        return "<office:document-settings" +
            CommonNamespaceAttributes +
            " office:version=\"" + version + "\"><office:settings><config:config-item-set config:name=\"ooo:view-settings\"><config:config-item config:name=\"VisibleAreaTop\" config:type=\"int\">0</config:config-item></config:config-item-set></office:settings></office:document-settings>";
    }

    private static string GetMimeType(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.Text => "application/vnd.oasis.opendocument.text",
            OdfDocumentKind.TextTemplate => "application/vnd.oasis.opendocument.text-template",
            OdfDocumentKind.TextMaster => "application/vnd.oasis.opendocument.text-master",
            OdfDocumentKind.Spreadsheet => "application/vnd.oasis.opendocument.spreadsheet",
            OdfDocumentKind.SpreadsheetTemplate => "application/vnd.oasis.opendocument.spreadsheet-template",
            OdfDocumentKind.Presentation => "application/vnd.oasis.opendocument.presentation",
            OdfDocumentKind.PresentationTemplate => "application/vnd.oasis.opendocument.presentation-template",
            OdfDocumentKind.Graphics => "application/vnd.oasis.opendocument.graphics",
            OdfDocumentKind.GraphicsTemplate => "application/vnd.oasis.opendocument.graphics-template",
            OdfDocumentKind.Chart => "application/vnd.oasis.opendocument.chart",
            OdfDocumentKind.Formula => "application/vnd.oasis.opendocument.formula",
            OdfDocumentKind.Image => "application/vnd.oasis.opendocument.image",
            OdfDocumentKind.Database => "application/vnd.oasis.opendocument.database",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支援的 ODF 文件類型。")
        };
    }

    private static string GetBodyElementName(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.Text or OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or OdfDocumentKind.FlatText => "text",
            OdfDocumentKind.Spreadsheet or OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet => "spreadsheet",
            OdfDocumentKind.Presentation or OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation => "presentation",
            OdfDocumentKind.Graphics or OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics => "drawing",
            OdfDocumentKind.Chart or OdfDocumentKind.FlatChart => "chart",
            OdfDocumentKind.Formula or OdfDocumentKind.FlatFormula => "formula",
            OdfDocumentKind.Image or OdfDocumentKind.FlatImage => "image",
            OdfDocumentKind.Database => "database",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支援的 ODF 文件類型。")
        };
    }

    private static OdfDocumentKind GetPackagedKind(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.FlatText => OdfDocumentKind.Text,
            OdfDocumentKind.FlatSpreadsheet => OdfDocumentKind.Spreadsheet,
            OdfDocumentKind.FlatPresentation => OdfDocumentKind.Presentation,
            OdfDocumentKind.FlatGraphics => OdfDocumentKind.Graphics,
            OdfDocumentKind.FlatChart => OdfDocumentKind.Chart,
            OdfDocumentKind.FlatFormula => OdfDocumentKind.Formula,
            OdfDocumentKind.FlatImage => OdfDocumentKind.Image,
            _ => kind
        };
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "必須指定具體的 ODF 版本。")
        };
    }

    private static void WriteCommonNamespaces(XmlWriter writer)
    {
        writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
        writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
        writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
        writer.WriteAttributeString("xmlns", "draw", null, OdfNamespaces.Draw);
        writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);
        writer.WriteAttributeString("xmlns", "xlink", null, OdfNamespaces.XLink);
        writer.WriteAttributeString("xmlns", "dc", null, OdfNamespaces.Dc);
        writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);
        writer.WriteAttributeString("xmlns", "number", null, OdfNamespaces.Number);
        writer.WriteAttributeString("xmlns", "presentation", null, OdfNamespaces.Presentation);
        writer.WriteAttributeString("xmlns", "svg", null, OdfNamespaces.Svg);
        writer.WriteAttributeString("xmlns", "chart", null, OdfNamespaces.Chart);
        writer.WriteAttributeString("xmlns", "config", null, OdfNamespaces.Config);
    }

    private const string CommonNamespaceAttributes =
        " xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"" +
        " xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\"" +
        " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"" +
        " xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"" +
        " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"" +
        " xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"" +
        " xmlns:xlink=\"http://www.w3.org/1999/xlink\"" +
        " xmlns:dc=\"http://purl.org/dc/elements/1.1/\"" +
        " xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\"" +
        " xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\"" +
        " xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\"" +
        " xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"" +
        " xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\"" +
        " xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\"";

    private static OdfDocumentKind DetectDocumentKind(OdfPackage package, string? fileName)
    {
        OdfDocumentKind extensionKind = OdfDocumentKindDetector.FromFileName(fileName);
        OdfDocumentKind mimeKind = OdfDocumentKindDetector.FromMimeType(package.MimeType);

        if (package.IsFlatXml)
        {
            if (OdfDocumentKindDetector.IsFlatKind(extensionKind))
            {
                return extensionKind;
            }

            if (mimeKind != OdfDocumentKind.Unknown)
            {
                return OdfDocumentKindDetector.ToFlatKind(mimeKind);
            }
        }

        if (mimeKind != OdfDocumentKind.Unknown)
        {
            return mimeKind;
        }

        OdfValidationReport report = OdfPackageValidator.Validate(package, fileName: fileName);

        return report.DocumentKind != OdfDocumentKind.Unknown
            ? report.DocumentKind
            : extensionKind;
    }

    private static OdfDocument CreateDocumentWrapper(OdfPackage package, OdfDocumentKind kind)
    {
        return OdfDocumentKindDetector.ToContentKind(kind) switch
        {
            OdfDocumentKind.Text => new TextDocument(package),
            OdfDocumentKind.Spreadsheet => new SpreadsheetDocument(package),
            OdfDocumentKind.Presentation => new PresentationDocument(package),
            OdfDocumentKind.Graphics => new DrawingDocument(package),
            OdfDocumentKind.Chart => new ChartDocument(package),
            OdfDocumentKind.Formula => new FormulaDocument(package),
            OdfDocumentKind.Image => new OdfImageDocument(package),
            OdfDocumentKind.Database => new OdfDatabaseDocument(package),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支援的 ODF 文件類型。")
        };
    }
}

