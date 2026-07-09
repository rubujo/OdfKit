#pragma warning restore CS1591

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
/// Creates minimal low-level ODF packages and flat XML documents.
/// 建立最小的低階 ODF 封裝與扁平 XML 文件。
/// </summary>
public static class OdfDocumentFactory
{
    /// <summary>
    /// Creates a high-level ODF document wrapper of the specified kind.
    /// 建立指定種類的高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="kind">The ODF document kind to create. / 要建立的 ODF 文件種類。</param>
    /// <returns>The created ODF document. / 建立完成的 ODF 文件。</returns>
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
    /// Loads a high-level ODF document wrapper from the specified path.
    /// 從指定路徑載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="path">The ODF document path. / ODF 文件路徑。</param>
    /// <returns>The loaded ODF document. / 載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(string path)
    {
        return LoadDocument(path, options: null);
    }

    /// <summary>
    /// Loads a high-level ODF document wrapper from the specified path and load options.
    /// 從指定路徑與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="path">The ODF document path. / ODF 文件路徑。</param>
    /// <param name="options">The load options, such as a password for encrypted documents and security limits. / 載入選項，例如加密文件密碼與安全限制。</param>
    /// <returns>The loaded ODF document. / 載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(string path, OdfLoadOptions? options)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        OdfPackage package = OdfPackage.Open(path, options);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, path));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODF document wrapper from the specified path.
    /// 非同步從指定路徑載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded ODF document. / 代表非同步載入作業的工作，其結果為載入完成的 ODF 文件。</returns>
    public static Task<OdfDocument> LoadDocumentAsync(string path) => LoadDocumentAsync(path, null, default);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts path and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static Task<OdfDocument> LoadDocumentAsync(string path, CancellationToken cancellationToken) => LoadDocumentAsync(path, null, cancellationToken);

    /// <summary>
    /// Asynchronously loads a high-level ODF document wrapper from the specified path and load options.
    /// 非同步從指定路徑與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded ODF document. / 代表非同步載入作業的工作，其結果為載入完成的 ODF 文件。</returns>
    public static Task<OdfDocument> LoadDocumentAsync(string path, OdfLoadOptions? options) => LoadDocumentAsync(path, options, default);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts path, options, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path、options 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static async Task<OdfDocument> LoadDocumentAsync(
        string path,
        OdfLoadOptions? options,
        CancellationToken cancellationToken)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        OdfPackage package = await OdfPackage.OpenAsync(path, options, cancellationToken).ConfigureAwait(false);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, path));
    }

    /// <summary>
    /// Loads a high-level ODF document wrapper from the specified stream.
    /// 從指定資料流載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <returns>The loaded ODF document. / 載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(Stream stream) => LoadDocument(stream, null, null);

    /// <summary>
    /// Short overload of LoadDocument that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadDocument 多載。
    /// </summary>
    public static OdfDocument LoadDocument(Stream stream, string? fileName) => LoadDocument(stream, null, fileName);

    /// <summary>
    /// Short overload of LoadDocument that accepts stream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 options；其餘可選參數使用預設值並轉呼叫最長 LoadDocument 多載。
    /// </summary>
    public static OdfDocument LoadDocument(Stream stream, OdfLoadOptions? options) => LoadDocument(stream, options, null);

    /// <summary>
    /// Loads a high-level ODF document wrapper from the specified stream and load options.
    /// 從指定資料流與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="stream">The stream containing the ODF document content. / 包含 ODF 文件內容的資料流。</param>
    /// <param name="options">The load options, such as a password for encrypted documents and security limits. / 載入選項，例如加密文件密碼與安全限制。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded ODF document. / 載入完成的 ODF 文件。</returns>
    public static OdfDocument LoadDocument(Stream stream, OdfLoadOptions? options, string? fileName)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        OdfPackage package = OdfPackage.Open(stream, leaveOpen: false, options: options);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODF document wrapper from the specified stream.
    /// 非同步從指定資料流載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded ODF document. / 代表非同步載入作業的工作，其結果為載入完成的 ODF 文件。</returns>
    public static Task<OdfDocument> LoadDocumentAsync(Stream stream) => LoadDocumentAsync(stream, null, null, default);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static Task<OdfDocument> LoadDocumentAsync(Stream stream, string? fileName) => LoadDocumentAsync(stream, null, fileName, default);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts stream, fileName, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、fileName 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static Task<OdfDocument> LoadDocumentAsync(Stream stream, string? fileName, CancellationToken cancellationToken) => LoadDocumentAsync(stream, null, fileName, cancellationToken);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts stream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 options；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static Task<OdfDocument> LoadDocumentAsync(Stream stream, OdfLoadOptions? options) => LoadDocumentAsync(stream, options, null, default);

    /// <summary>
    /// Short overload of LoadDocumentAsync that accepts stream, options, and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、options 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadDocumentAsync 多載。
    /// </summary>
    public static Task<OdfDocument> LoadDocumentAsync(Stream stream, OdfLoadOptions? options, string? fileName) => LoadDocumentAsync(stream, options, fileName, default);

    /// <summary>
    /// Asynchronously loads a high-level ODF document wrapper from the specified stream and load options.
    /// 非同步從指定資料流與載入選項載入高階 ODF 文件 wrapper。
    /// </summary>
    /// <param name="stream">The stream containing the ODF document content. / 包含 ODF 文件內容的資料流。</param>
    /// <param name="options">The load options, such as a password for encrypted documents and security limits. / 載入選項，例如加密文件密碼與安全限制。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded ODF document. / 代表非同步載入作業的工作，其結果為載入完成的 ODF 文件。</returns>
    public static async Task<OdfDocument> LoadDocumentAsync(
        Stream stream,
        OdfLoadOptions? options,
        string? fileName,
        CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        OdfPackage package = await OdfPackage.OpenAsync(stream, leaveOpen: false, options: options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return CreateDocumentWrapper(package, DetectDocumentKind(package, fileName));
    }

    /// <summary>
    /// Creates a minimally packaged ODF document in the provided stream.
    /// 在提供的資料流中建立一個最小封裝的 ODF 文件。
    /// </summary>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 傳回建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage CreatePackage(Stream stream, OdfDocumentKind kind) => CreatePackage(stream, kind, OdfVersion.Odf14, false, null);

    /// <summary>
    /// Creates a minimally packaged ODF document in the provided stream with an explicit leave-open flag.
    /// 在提供的資料流中建立最小封裝 ODF 文件，並明確指定是否保持資料流開啟。
    /// </summary>
    /// <param name="stream">The destination stream. / 目標資料流。</param>
    /// <param name="kind">The ODF document kind. / ODF 文件種類。</param>
    /// <param name="leaveOpen">Whether to leave the stream open after disposing the package. / 封裝釋放後是否保持資料流開啟。</param>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 傳回建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage CreatePackage(Stream stream, OdfDocumentKind kind, bool leaveOpen) =>
        CreatePackage(stream, kind, OdfVersion.Odf14, leaveOpen, null);

    /// <summary>
    /// Short overload of CreatePackage that accepts stream, kind, and version; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、kind 與 version；其餘可選參數使用預設值並轉呼叫最長 CreatePackage 多載。
    /// </summary>
    public static OdfPackage CreatePackage(Stream stream, OdfDocumentKind kind, OdfVersion version) => CreatePackage(stream, kind, version, false, null);

    /// <summary>
    /// Short overload of CreatePackage that accepts stream, kind, version, and leaveOpen; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、kind、version 與 leaveOpen；其餘可選參數使用預設值並轉呼叫最長 CreatePackage 多載。
    /// </summary>
    public static OdfPackage CreatePackage(Stream stream, OdfDocumentKind kind, OdfVersion version, bool leaveOpen) => CreatePackage(stream, kind, version, leaveOpen, null);

    /// <summary>
    /// Short overload of CreatePackage that accepts stream, kind, version, leaveOpen, and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、kind、version、leaveOpen 與 options；其餘可選參數使用預設值並轉呼叫最長 CreatePackage 多載。
    /// </summary>
    public static OdfPackage CreatePackage(
        Stream stream,
        OdfDocumentKind kind,
        OdfVersion version,
        bool leaveOpen,
        OdfSaveOptions? options)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (OdfDocumentKindDetector.IsFlatKind(kind))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocumentFactory_FlatOdfTypesCreated_2"), nameof(kind));

        var package = OdfPackage.Create(stream, leaveOpen, options);
        InitializeMinimalPackage(package, kind, version);
        return package;
    }

    /// <summary>
    /// Creates a minimally packaged ODF document at the provided path.
    /// 在提供的路徑上建立一個最小封裝的 ODF 文件。
    /// </summary>
    /// <returns>The created <see cref="OdfPackage"/> instance. / 傳回建立的 <see cref="OdfPackage"/> 執行個體。</returns>
    public static OdfPackage CreatePackage(string path, OdfDocumentKind kind) => CreatePackage(path, kind, OdfVersion.Odf14, null);

    /// <summary>
    /// Short overload of CreatePackage that accepts path, kind, and version; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path、kind 與 version；其餘可選參數使用預設值並轉呼叫最長 CreatePackage 多載。
    /// </summary>
    public static OdfPackage CreatePackage(string path, OdfDocumentKind kind, OdfVersion version) => CreatePackage(path, kind, version, null);

    /// <summary>
    /// Short overload of CreatePackage that accepts path, kind, version, and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path、kind、version 與 options；其餘可選參數使用預設值並轉呼叫最長 CreatePackage 多載。
    /// </summary>
    public static OdfPackage CreatePackage(
        string path,
        OdfDocumentKind kind,
        OdfVersion version,
        OdfSaveOptions? options)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (OdfDocumentKindDetector.IsFlatKind(kind))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocumentFactory_FlatOdfTypesCreated_2"), nameof(kind));

        var package = OdfPackage.Create(path, options);
        InitializeMinimalPackage(package, kind, version);
        return package;
    }

    /// <summary>
    /// Writes a minimal flat XML ODF document with default write options.
    /// 以預設寫入選項寫入最小 Flat XML ODF 文件。
    /// </summary>
    /// <param name="stream">The destination stream. / 目的串流。</param>
    /// <param name="kind">The ODF document kind. / ODF 文件種類。</param>
    public static void WriteFlatXml(Stream stream, OdfDocumentKind kind) =>
        WriteFlatXml(stream, kind, OdfFlatXmlWriteOptions.Default);

    /// <summary>
    /// Writes a minimal flat XML ODF document using write options.
    /// 以寫入選項寫入最小 Flat XML ODF 文件。
    /// </summary>
    /// <param name="stream">The destination stream. / 目的串流。</param>
    /// <param name="kind">The ODF document kind. / ODF 文件種類。</param>
    /// <param name="options">The flat XML write options. / Flat XML 寫入選項。</param>
    public static void WriteFlatXml(Stream stream, OdfDocumentKind kind, OdfFlatXmlWriteOptions options)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        OdfDocumentKind flatKind = OdfDocumentKindDetector.ToFlatKind(kind);
        if (!OdfDocumentKindDetector.IsFlatKind(flatKind))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocumentFactory_ProvidedFileTypeFlat"), nameof(kind));
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = !options.LeaveOpen
        };

        using XmlWriter writer = XmlWriter.Create(stream, settings);
        string mimeType = GetMimeType(GetPackagedKind(flatKind));
        string versionText = FormatVersion(options.Version);
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
    /// Short overload of InitializeMinimalPackage that accepts package and kind; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 package 與 kind；其餘可選參數使用預設值並轉呼叫最長 InitializeMinimalPackage 多載。
    /// </summary>
    public static void InitializeMinimalPackage(OdfPackage package, OdfDocumentKind kind) => InitializeMinimalPackage(package, kind, OdfVersion.Odf14);


    /// <summary>
    /// Populates the package with a minimal ODF entity of the specified document kind.
    /// 以指定的文件類型在封裝中填入最小的 ODF 實體。
    /// </summary>
    /// <param name="package">The OdfPackage instance to initialize. / 要初始化的 OdfPackage 執行個體。</param>
    /// <param name="kind">The ODF document kind. / ODF 文件的類型。</param>
    /// <param name="version">The ODF specification version. / ODF 規格版本。</param>
    public static void InitializeMinimalPackage(OdfPackage package, OdfDocumentKind kind, OdfVersion version)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));
        if (OdfDocumentKindDetector.IsFlatKind(kind))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocumentFactory_FlatOdfTypesCannot"), nameof(kind));

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
        kind == OdfDocumentKind.TextMaster ||
        kind == OdfDocumentKind.TextWeb;

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
            OdfDocumentKind.ChartTemplate => "application/vnd.oasis.opendocument.chart-template",
            OdfDocumentKind.Formula => "application/vnd.oasis.opendocument.formula",
            OdfDocumentKind.FormulaTemplate => "application/vnd.oasis.opendocument.formula-template",
            OdfDocumentKind.Image => "application/vnd.oasis.opendocument.image",
            OdfDocumentKind.ImageTemplate => "application/vnd.oasis.opendocument.image-template",
            OdfDocumentKind.Database => "application/vnd.oasis.opendocument.base",
            OdfDocumentKind.TextWeb => "application/vnd.oasis.opendocument.text-web",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, OdfLocalizer.GetMessage("Err_OdfDocumentFactory_UnsupportedOdfFileType_3"))
        };
    }

    private static string GetBodyElementName(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.Text or OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or OdfDocumentKind.TextWeb or OdfDocumentKind.FlatText => "text",
            OdfDocumentKind.Spreadsheet or OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet => "spreadsheet",
            OdfDocumentKind.Presentation or OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation => "presentation",
            OdfDocumentKind.Graphics or OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics => "drawing",
            OdfDocumentKind.Chart or OdfDocumentKind.ChartTemplate or OdfDocumentKind.FlatChart => "chart",
            OdfDocumentKind.Formula or OdfDocumentKind.FormulaTemplate or OdfDocumentKind.FlatFormula => "formula",
            OdfDocumentKind.Image or OdfDocumentKind.ImageTemplate or OdfDocumentKind.FlatImage => "image",
            OdfDocumentKind.Database => "database",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, OdfLocalizer.GetMessage("Err_OdfDocumentFactory_UnsupportedOdfFileType_3"))
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
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, OdfLocalizer.GetMessage("Err_OdfDocumentFactory_SpecificOdfVersionSpecified"))
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

        OdfValidationReport report = OdfPackageValidator.Validate(
            package,
            new OdfValidationOptions { FileName = fileName });

        return report.DocumentKind != OdfDocumentKind.Unknown
            ? report.DocumentKind
            : extensionKind;
    }

    internal static OdfDocument CreateDocumentWrapper(OdfPackage package, OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.Text => new TextDocument(package),
            OdfDocumentKind.TextTemplate => new TextTemplateDocument(package),
            OdfDocumentKind.TextMaster => new TextMasterDocument(package),
            OdfDocumentKind.FlatText => new FlatTextDocument(package),
            OdfDocumentKind.Spreadsheet => new SpreadsheetDocument(package),
            OdfDocumentKind.SpreadsheetTemplate => new SpreadsheetTemplateDocument(package),
            OdfDocumentKind.FlatSpreadsheet => new FlatSpreadsheetDocument(package),
            OdfDocumentKind.Presentation => new PresentationDocument(package),
            OdfDocumentKind.PresentationTemplate => new PresentationTemplateDocument(package),
            OdfDocumentKind.FlatPresentation => new FlatPresentationDocument(package),
            OdfDocumentKind.Graphics => new DrawingDocument(package),
            OdfDocumentKind.GraphicsTemplate => new GraphicsTemplateDocument(package),
            OdfDocumentKind.FlatGraphics => new FlatGraphicsDocument(package),
            OdfDocumentKind.Chart => new ChartDocument(package),
            OdfDocumentKind.ChartTemplate => new ChartTemplateDocument(package),
            OdfDocumentKind.FlatChart => new FlatChartDocument(package),
            OdfDocumentKind.Formula => new FormulaDocument(package),
            OdfDocumentKind.FormulaTemplate => new FormulaTemplateDocument(package),
            OdfDocumentKind.FlatFormula => new FlatFormulaDocument(package),
            OdfDocumentKind.Image => new ImageDocument(package),
            OdfDocumentKind.ImageTemplate => new ImageTemplateDocument(package),
            OdfDocumentKind.FlatImage => new FlatImageDocument(package),
            OdfDocumentKind.Database => new DatabaseDocument(package),
            OdfDocumentKind.TextWeb => new TextWebDocument(package),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, OdfLocalizer.GetMessage("Err_OdfDocumentFactory_UnsupportedOdfFileType_3"))
        };
    }
}

