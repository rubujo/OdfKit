using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents an ODF presentation document.
/// 表示 ODF 簡報文件（Presentation Document）的類別。
/// </summary>
public partial class PresentationDocument : OdfDocument
{
    private readonly List<OdfSlide> _slides = [];
    private OdfSlideCollection? _slideCollection;

    /// <summary>
    /// Gets the slide collection.
    /// 取得投影片集合。
    /// </summary>
    public OdfSlideCollection Slides => _slideCollection ??= new OdfSlideCollection(this);

    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationDocument"/> class.
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    public PresentationDocument() : this(OdfPackage.Create(new MemoryStream()))
    {
        Package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        Package.SaveManifestToEntries();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationDocument"/> class.
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / Odf 套件執行個體。</param>
    public PresentationDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        }
        ParseSlides();
    }

    /// <summary>
    /// Creates a new ODP presentation document fluent builder.
    /// 建立新的 ODP 簡報文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="PresentationDocumentBuilder"/> instance. / 新的 <see cref="PresentationDocumentBuilder"/> 執行個體。</returns>
    public static PresentationDocumentBuilder Builder()
    {
        return new PresentationDocumentBuilder(Create());
    }

    /// <summary>
    /// Creates a new ODP presentation document.
    /// 建立新的 ODP 簡報文件。
    /// </summary>
    /// <returns>A new <see cref="PresentationDocument"/> instance. / 新的 <see cref="PresentationDocument"/> 執行個體。</returns>
    public static PresentationDocument Create()
    {
        return (PresentationDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Presentation);
    }
    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static PresentationDocument CreateFromTemplate(PresentationTemplateDocument template) => CreateFromTemplate(template, false);


    /// <summary>
    /// Creates a new presentation document from the specified presentation template.
    /// 從指定的簡報範本文件建立新的簡報文件。
    /// </summary>
    /// <param name="template">The presentation template document. / 簡報範本文件。</param>
    /// <param name="clearUserContent">Whether to clear the text content of each slide in the template while keeping layout and shape structure. / 是否清除範本中各投影片的文字內容，但保留版面配置與形狀結構。</param>
    /// <returns>The created <see cref="PresentationDocument"/> instance. / 建立完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    public static PresentationDocument CreateFromTemplate(PresentationTemplateDocument template, bool clearUserContent)
    {
        return (PresentationDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Presentation, "application/vnd.oasis.opendocument.presentation", clearUserContent);
    }


    /// <summary>
    /// Creates an equivalent ODP (ZIP package) presentation document from a FODP flat XML presentation document, with identical content.
    /// 從 FODP 扁平 XML 簡報文件建立等價的 ODP（ZIP 封裝）簡報文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODP flat XML presentation document. / 來源 FODP 扁平 XML 簡報文件。</param>
    /// <returns>The created <see cref="PresentationDocument"/> instance. / 建立完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    public static PresentationDocument CreateFromFlatDocument(FlatPresentationDocument document) =>
        (PresentationDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Presentation, targetIsFlatXml: false);

    /// <summary>
    /// Executes the ClearTemplateUserContent operation.
    /// 執行 ClearTemplateUserContent 作業。
    /// </summary>
    /// <inheritdoc/>
    protected override void ClearTemplateUserContent()
    {
        ClearParagraphTextContentRecursive(GetPresentationNode());
    }

    /// <summary>
    /// Loads an ODP presentation document from the specified path.
    /// 從指定路徑載入 ODP 簡報文件。
    /// </summary>
    /// <param name="path">The ODP document path. / ODP 文件路徑。</param>
    /// <returns>The loaded <see cref="PresentationDocument"/> instance. / 載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODP presentation. / 當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(string path) =>
        OdfDocumentVariantSupport.Load<PresentationDocument>(path, OdfDocumentKind.Presentation, "Err_PresentationDocument_SpecifiedOdfFileOdp");

    /// <summary>
    /// Asynchronously loads an ODP presentation document from the specified path.
    /// 非同步從指定路徑載入 ODP 簡報文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="PresentationDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationDocument"/>。</returns>
    public new static Task<PresentationDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public new static Task<PresentationDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        OdfDocumentVariantSupport.LoadAsync<PresentationDocument>(path, OdfDocumentKind.Presentation, "Err_PresentationDocument_SpecifiedOdfFileOdp", cancellationToken);

    /// <summary>
    /// Loads an ODP presentation document from the specified stream.
    /// 從指定資料流載入 ODP 簡報文件。
    /// </summary>
    /// <returns>The loaded <see cref="PresentationDocument"/> instance. / 載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODP presentation. / 當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public new static PresentationDocument Load(Stream stream, string? fileName) =>
        OdfDocumentVariantSupport.Load<PresentationDocument>(stream, OdfDocumentKind.Presentation, "Err_PresentationDocument_SpecifiedOdfFileOdp", fileName);

    /// <summary>
    /// Asynchronously loads an ODP presentation document from the specified stream.
    /// 非同步從指定資料流載入 ODP 簡報文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="PresentationDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationDocument"/>。</returns>
    public new static Task<PresentationDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public new static Task<PresentationDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public new static Task<PresentationDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public new static Task<PresentationDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        OdfDocumentVariantSupport.LoadAsync<PresentationDocument>(stream, OdfDocumentKind.Presentation, "Err_PresentationDocument_SpecifiedOdfFileOdp", fileName, cancellationToken);

    internal IReadOnlyList<OdfSlide> GetSlidesSnapshot()
    {
        return _slides.AsReadOnly();
    }

    private void ParseSlides()
    {
        _slides.Clear();
        var presentationNode = GetPresentationNode();
        foreach (var child in presentationNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "page" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                _slides.Add(new OdfSlide(child, this));
            }
        }
    }
}
