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
    /// <param name="package">ODF 封裝</param>
    public TextTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTT 文字範本文件。
    /// </summary>
    /// <returns>新的 <see cref="TextTemplateDocument"/> 執行個體</returns>
    public static new TextTemplateDocument Create()
    {
        return (TextTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="path">OTT 文件路徑</param>
    /// <returns>載入完成的 <see cref="TextTemplateDocument"/> 執行個體</returns>
    public static new TextTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="path">OTT 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextTemplateDocument"/></returns>
    public static new async Task<TextTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="stream">包含 OTT 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="TextTemplateDocument"/> 執行個體</returns>
    public static new TextTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTT 文字範本文件。
    /// </summary>
    /// <param name="stream">包含 OTT 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextTemplateDocument"/></returns>
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
    /// <param name="package">ODF 封裝</param>
    public TextMasterDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 ODM 主控文字文件。
    /// </summary>
    /// <returns>新的 <see cref="TextMasterDocument"/> 執行個體</returns>
    public static new TextMasterDocument Create()
    {
        return (TextMasterDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextMaster);
    }

    /// <summary>
    /// 從指定路徑載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="path">ODM 文件路徑</param>
    /// <returns>載入完成的 <see cref="TextMasterDocument"/> 執行個體</returns>
    public static new TextMasterDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="path">ODM 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextMasterDocument"/></returns>
    public static new async Task<TextMasterDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="stream">包含 ODM 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="TextMasterDocument"/> 執行個體</returns>
    public static new TextMasterDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 ODM 主控文字文件。
    /// </summary>
    /// <param name="stream">包含 ODM 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextMasterDocument"/></returns>
    public static new async Task<TextMasterDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 取得文件中所有指向外部子文件的區段參照。
    /// </summary>
    /// <returns>子文件參照清單</returns>
    public IReadOnlyList<OdfSubDocumentReference> GetSubDocumentReferences()
    {
        var results = new List<OdfSubDocumentReference>();
        CollectSubDocumentReferences(BodyTextRoot, results);
        return results;
    }

    /// <summary>
    /// 將主控文件本身內容與所有外部子文件依文件順序合併為單一文字文件。
    /// </summary>
    /// <param name="baseDirectory">解析子文件參照相對路徑時所使用的基準目錄。</param>
    /// <param name="options">合併設定選項；若為 <see langword="null"/> 則使用預設選項。</param>
    /// <returns>合併完成的新 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentException">當 <paramref name="baseDirectory"/> 為空白時擲出。</exception>
    public TextDocument MergeSubDocuments(string baseDirectory, OdfMergeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextMasterDocument_BaseDirectoryCannotBeEmpty"), nameof(baseDirectory));
        }

        TextDocument merged = TextDocument.Create();
        foreach (OdfNode child in new List<OdfNode>(BodyTextRoot.Children))
        {
            OdfNode? sectionSource = child.NodeType is OdfNodeType.Element &&
                child.LocalName == "section" &&
                child.NamespaceUri == OdfNamespaces.Text
                ? child.FindChildElement("section-source", OdfNamespaces.Text)
                : null;
            string? href = sectionSource?.GetAttribute("href", OdfNamespaces.XLink);

            if (!string.IsNullOrEmpty(href))
            {
                string fullPath = Path.Combine(baseDirectory, href);
                using TextDocument subDoc = TextDocument.Load(fullPath);
                merged.AppendDocument(subDoc, options);
            }
            else
            {
                OdfNode imported = OdfNode.ImportNode(child, Package, merged.Package);
                merged.BodyTextRoot.AppendChild(imported);
            }
        }

        return merged;
    }

    /// <summary>
    /// 設定指定名稱外部子文件區段參照的載入時機。
    /// </summary>
    /// <param name="sectionName">區段名稱</param>
    /// <param name="loadOnRequest">
    /// <see langword="true"/> 表示延遲載入（<c>xlink:actuate="onRequest"</c>）；
    /// <see langword="false"/> 表示開啟主控文件時立即載入（<c>xlink:actuate="onLoad"</c>）。
    /// </param>
    /// <returns>若成功設定則為 <see langword="true"/>；找不到對應名稱的子文件參照時為 <see langword="false"/></returns>
    /// <exception cref="ArgumentException">當 <paramref name="sectionName"/> 為空白時擲出</exception>
    public bool SetSubDocumentLoadOnRequest(string sectionName, bool loadOnRequest)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentVariants_SectionCannotBeEmpty_2"), nameof(sectionName));
        }

        OdfNode? section = FindSubDocumentSectionNode(BodyTextRoot, sectionName);
        OdfNode? sectionSource = section?.FindChildElement("section-source", OdfNamespaces.Text);
        if (sectionSource is null)
        {
            return false;
        }

        sectionSource.SetAttribute("actuate", OdfNamespaces.XLink, loadOnRequest ? "onRequest" : "onLoad", "xlink");
        return true;
    }

    /// <summary>
    /// 移除指定名稱的外部子文件區段參照。
    /// </summary>
    /// <param name="sectionName">區段名稱</param>
    /// <returns>若成功移除則為 <see langword="true"/>；找不到對應名稱的子文件參照時為 <see langword="false"/></returns>
    /// <exception cref="ArgumentException">當 <paramref name="sectionName"/> 為空白時擲出</exception>
    public bool RemoveSubDocumentReference(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentVariants_SectionCannotBeEmpty_2"), nameof(sectionName));
        }

        OdfNode? section = FindSubDocumentSectionNode(BodyTextRoot, sectionName);
        if (section?.Parent is null)
        {
            return false;
        }

        section.Parent.RemoveChild(section);
        return true;
    }

    /// <summary>
    /// 依指定名稱順序重新排列外部子文件區段參照。
    /// </summary>
    /// <param name="orderedSectionNames">期望的區段名稱排列順序。找不到的名稱會被忽略</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="orderedSectionNames"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當指定的子文件參照分散於不同父節點下，無法重新排序時擲出</exception>
    public void ReorderSubDocumentReferences(IReadOnlyList<string> orderedSectionNames)
    {
        if (orderedSectionNames is null)
        {
            throw new ArgumentNullException(nameof(orderedSectionNames));
        }

        List<OdfNode> sections = [];
        OdfNode? parent = null;
        foreach (string name in orderedSectionNames)
        {
            OdfNode? section = FindSubDocumentSectionNode(BodyTextRoot, name);
            if (section?.Parent is null)
            {
                continue;
            }

            if (parent is null)
            {
                parent = section.Parent;
            }
            else if (!ReferenceEquals(parent, section.Parent))
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_TextDocumentVariants_SectionUnderSameParent", name));
            }

            sections.Add(section);
        }

        if (parent is null || sections.Count == 0)
        {
            return;
        }

        List<OdfNode> originalSiblings = new(parent.Children);
        int earliestIndex = int.MaxValue;
        foreach (OdfNode section in sections)
        {
            int index = originalSiblings.IndexOf(section);
            if (index >= 0 && index < earliestIndex)
            {
                earliestIndex = index;
            }
        }

        OdfNode? insertBeforeAnchor = null;
        for (int i = earliestIndex + 1; i < originalSiblings.Count; i++)
        {
            if (!sections.Contains(originalSiblings[i]))
            {
                insertBeforeAnchor = originalSiblings[i];
                break;
            }
        }

        foreach (OdfNode section in sections)
        {
            parent.RemoveChild(section);
        }

        foreach (OdfNode section in sections)
        {
            if (insertBeforeAnchor is not null)
            {
                parent.InsertBefore(section, insertBeforeAnchor);
            }
            else
            {
                parent.AppendChild(section);
            }
        }
    }

    private static OdfNode? FindSubDocumentSectionNode(OdfNode node, string sectionName)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "section" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                HasSectionSource(child) &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Text), sectionName, StringComparison.Ordinal))
            {
                return child;
            }

            OdfNode? found = FindSubDocumentSectionNode(child, sectionName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool HasSectionSource(OdfNode sectionNode)
    {
        foreach (OdfNode child in sectionNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "section-source" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                return true;
            }
        }

        return false;
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
                        string actuate = sectionChild.GetAttribute("actuate", OdfNamespaces.XLink) is { Length: > 0 } actuateAttr
                            ? actuateAttr
                            : "onLoad";
                        if (name is { Length: > 0 } sectionName && href is { Length: > 0 } sectionHref)
                        {
                            results.Add(new OdfSubDocumentReference(sectionName, sectionHref, actuate));
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
    /// <param name="package">ODF 封裝</param>
    public TextWebDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTH 網頁範本文件。
    /// </summary>
    /// <returns>新的 <see cref="TextWebDocument"/> 執行個體</returns>
    public static new TextWebDocument Create()
    {
        return (TextWebDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.TextWeb);
    }

    /// <summary>
    /// 從指定路徑載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="path">OTH 文件路徑</param>
    /// <returns>載入完成的 <see cref="TextWebDocument"/> 執行個體</returns>
    public static new TextWebDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="path">OTH 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextWebDocument"/></returns>
    public static new async Task<TextWebDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="stream">包含 OTH 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="TextWebDocument"/> 執行個體</returns>
    public static new TextWebDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTH 網頁範本文件。
    /// </summary>
    /// <param name="stream">包含 OTH 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextWebDocument"/></returns>
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
    /// <param name="package">ODF 封裝或扁平 XML 容器</param>
    public FlatTextDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatTextDocument"/> 執行個體</returns>
    public static new FlatTextDocument Create()
    {
        return (FlatTextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatText);
    }

    /// <summary>
    /// 從指定路徑載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="path">FODT 文件路徑</param>
    /// <returns>載入完成的 <see cref="FlatTextDocument"/> 執行個體</returns>
    public static new FlatTextDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="path">FODT 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatTextDocument"/></returns>
    public static new async Task<FlatTextDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="stream">包含 FODT 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="FlatTextDocument"/> 執行個體</returns>
    public static new FlatTextDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODT 扁平 XML 文字文件。
    /// </summary>
    /// <param name="stream">包含 FODT 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatTextDocument"/></returns>
    public static new async Task<FlatTextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatTextDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatTextDocument>(
            document,
            OdfDocumentKind.FlatText,
            "指定的 ODF 文件不是 FODT 扁平 XML 文字文件。");
}
