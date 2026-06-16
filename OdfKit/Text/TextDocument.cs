using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文字文件。
/// </summary>
public partial class TextDocument : OdfDocument
{
    private OdfTextBody? _body;
    private OdfDocumentMetadata? _metadata;
    private int _footnoteCounter;
    private int _endnoteCounter;

    /// <summary>
    /// 取得或設定文字文件的本文根節點。
    /// </summary>
    public OdfNode BodyTextRoot { get; private set; } = null!;

    /// <summary>
    /// 初始化 <see cref="TextDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">OdfPackage 封裝包執行個體</param>
    public TextDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
        }
        InitializeTextRoot();
        StyleEngine.OnStyleChanging = TrackFormatChange;
    }

    /// <summary>
    /// 建立新的 ODT 文字文件。
    /// </summary>
    /// <returns>新的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument Create()
    {
        return (TextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Text);
    }

    /// <summary>
    /// 建立新的 ODT 文字文件 Fluent builder。
    /// </summary>
    /// <returns>新的 <see cref="TextDocumentBuilder"/> 執行個體。</returns>
    public static TextDocumentBuilder Builder()
    {
        return new TextDocumentBuilder(Create());
    }

    /// <summary>
    /// 從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(string path)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static Task<TextDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(path), cancellationToken);
    }

    /// <summary>
    /// 從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static Task<TextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(stream, fileName), cancellationToken);
    }

    /// <summary>
    /// 取得文字文件本文的高階操作入口。
    /// </summary>
    public OdfTextBody Body => _body ??= new OdfTextBody(this);

    /// <summary>
    /// 取得文件中繼資料的高階操作入口。
    /// </summary>
    public OdfDocumentMetadata Metadata => _metadata ??= new OdfDocumentMetadata(this);

    private static TextDocument EnsureTextDocument(OdfDocument document)
    {
        if (document is TextDocument textDocument)
        {
            return textDocument;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODT 文字文件。");
    }

    private void InitializeTextRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        BodyTextRoot = FindOrCreateChild(body, "text", OdfNamespaces.Office, "office");
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" xmlns:form=\"urn:oasis:names:tc:opendocument:xmlns:form:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:text></office:text></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
    }

}
