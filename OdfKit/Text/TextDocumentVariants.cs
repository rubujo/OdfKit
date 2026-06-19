using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文字範本文件（OTT）。
/// </summary>
public sealed class TextTemplateDocument : TextDocument
{
    /// <summary>
    /// 初始化 <see cref="TextTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public TextTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTT 文字範本文件。
    /// </summary>
    /// <returns>新的 <see cref="TextTemplateDocument"/> 執行個體。</returns>
    public static new TextTemplateDocument Create()
    {
        return (TextTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="path">OTT 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextTemplateDocument"/> 執行個體。</returns>
    public static new TextTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="path">OTT 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextTemplateDocument"/>。</returns>
    public static new async Task<TextTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="stream">包含 OTT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextTemplateDocument"/> 執行個體。</returns>
    public static new TextTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="stream">包含 OTT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextTemplateDocument"/>。</returns>
    public static new async Task<TextTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static TextTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<TextTemplateDocument>(
            document,
            OdfDocumentKind.TextTemplate,
            "指定的 ODF 文件不是 OTT 文字範本。");
}

/// <summary>
/// 表示 ODF 主控文字文件（ODM）。
/// </summary>
public sealed class TextMasterDocument : TextDocument
{
    /// <summary>
    /// 初始化 <see cref="TextMasterDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public TextMasterDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 ODM 主控文字文件。
    /// </summary>
    /// <returns>新的 <see cref="TextMasterDocument"/> 執行個體。</returns>
    public static new TextMasterDocument Create()
    {
        return (TextMasterDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextMaster);
    }

    /// <summary>
    /// 從指定路徑載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="path">ODM 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextMasterDocument"/> 執行個體。</returns>
    public static new TextMasterDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="path">ODM 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextMasterDocument"/>。</returns>
    public static new async Task<TextMasterDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="stream">包含 ODM 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextMasterDocument"/> 執行個體。</returns>
    public static new TextMasterDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="stream">包含 ODM 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextMasterDocument"/>。</returns>
    public static new async Task<TextMasterDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 取得文件中所有指向外部子文件的區段參照。
    /// </summary>
    /// <returns>子文件參照清單。</returns>
    public IReadOnlyList<OdfSubDocumentReference> GetSubDocumentReferences()
    {
        var results = new List<OdfSubDocumentReference>();
        CollectSubDocumentReferences(BodyTextRoot, results);
        return results;
    }

    private static void CollectSubDocumentReferences(OdfNode node, List<OdfSubDocumentReference> results)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "section" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                foreach (OdfNode sectionChild in child.Children)
                {
                    if (sectionChild.NodeType is OdfNodeType.Element &&
                        sectionChild.LocalName == "section-source" &&
                        sectionChild.NamespaceUri == OdfNamespaces.Text)
                    {
                        string? name = child.GetAttribute("name", OdfNamespaces.Text);
                        string? href = sectionChild.GetAttribute("href", OdfNamespaces.XLink);
                        if (name is { Length: > 0 } sectionName && href is { Length: > 0 } sectionHref)
                        {
                            results.Add(new OdfSubDocumentReference(sectionName, sectionHref));
                        }
                    }
                }
            }

            CollectSubDocumentReferences(child, results);
        }
    }

    private static TextMasterDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<TextMasterDocument>(
            document,
            OdfDocumentKind.TextMaster,
            "指定的 ODF 文件不是 ODM 主控文字文件。");
}

/// <summary>
/// 表示 ODF 網頁範本文件（OTH）。
/// </summary>
public sealed class TextWebDocument : TextDocument
{
    /// <summary>
    /// 初始化 <see cref="TextWebDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public TextWebDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTH 網頁範本文件。
    /// </summary>
    /// <returns>新的 <see cref="TextWebDocument"/> 執行個體。</returns>
    public static new TextWebDocument Create()
    {
        return (TextWebDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextWeb);
    }

    /// <summary>
    /// 從指定路徑載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="path">OTH 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextWebDocument"/> 執行個體。</returns>
    public static new TextWebDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="path">OTH 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextWebDocument"/>。</returns>
    public static new async Task<TextWebDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="stream">包含 OTH 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextWebDocument"/> 執行個體。</returns>
    public static new TextWebDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="stream">包含 OTH 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextWebDocument"/>。</returns>
    public static new async Task<TextWebDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static TextWebDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<TextWebDocument>(
            document,
            OdfDocumentKind.TextWeb,
            "指定的 ODF 文件不是 OTH 網頁範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 文字文件（FODT）。
/// </summary>
public sealed class FlatTextDocument : TextDocument
{
    /// <summary>
    /// 初始化 <see cref="FlatTextDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public FlatTextDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatTextDocument"/> 執行個體。</returns>
    public static new FlatTextDocument Create()
    {
        return (FlatTextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatText);
    }

    /// <summary>
    /// 從指定路徑載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="path">FODT 文件路徑。</param>
    /// <returns>載入完成的 <see cref="FlatTextDocument"/> 執行個體。</returns>
    public static new FlatTextDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="path">FODT 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatTextDocument"/>。</returns>
    public static new async Task<FlatTextDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="stream">包含 FODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="FlatTextDocument"/> 執行個體。</returns>
    public static new FlatTextDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="stream">包含 FODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatTextDocument"/>。</returns>
    public static new async Task<FlatTextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatTextDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatTextDocument>(
            document,
            OdfDocumentKind.FlatText,
            "指定的 ODF 文件不是 FODT 扁平 XML 文字文件。");
}
