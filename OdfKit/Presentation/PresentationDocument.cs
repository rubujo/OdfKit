using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示 ODF 簡報文件（Presentation Document）的類別。
/// </summary>
public partial class PresentationDocument : OdfDocument
{
    private readonly List<OdfSlide> _slides = [];
    private OdfSlideCollection? _slideCollection;

    /// <summary>
    /// 取得投影片集合。
    /// </summary>
    public OdfSlideCollection Slides => _slideCollection ??= new OdfSlideCollection(this);

    /// <summary>
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    public PresentationDocument() : this(OdfPackage.Create(new MemoryStream()))
    {
        Package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        Package.SaveManifestToEntries();
    }

    /// <summary>
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件執行個體</param>
    public PresentationDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        }
        ParseSlides();
    }

    /// <summary>
    /// 建立新的 ODP 簡報文件。
    /// </summary>
    /// <returns>新的 <see cref="PresentationDocument"/> 執行個體。</returns>
    public static PresentationDocument Create()
    {
        return (PresentationDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Presentation);
    }

    /// <summary>
    /// 從指定路徑載入 ODP 簡報文件。
    /// </summary>
    /// <param name="path">ODP 文件路徑。</param>
    /// <returns>載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(string path)
    {
        return EnsurePresentation(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODP 簡報文件。
    /// </summary>
    /// <param name="stream">包含 ODP 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(Stream stream, string? fileName = null)
    {
        return EnsurePresentation(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    internal IReadOnlyList<OdfSlide> GetSlidesSnapshot()
    {
        return _slides.AsReadOnly();
    }

    private static PresentationDocument EnsurePresentation(OdfDocument document)
    {
        if (document is PresentationDocument presentation)
        {
            return presentation;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODP 簡報。");
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
