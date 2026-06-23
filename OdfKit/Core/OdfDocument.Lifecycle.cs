using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Package Lifecycle & Persistence

    /// <summary>
    /// 儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    /// <remarks>在 ASP.NET Core 等伺服器環境中，請優先使用 <see cref="SaveAsync(OdfSaveOptions?, CancellationToken)"/> 以避免阻塞執行緒</remarks>
    public virtual void Save(OdfSaveOptions? options = null)
    {
        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        Package.Save(options);
    }

    /// <summary>
    /// 將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑</param>
    /// <param name="options">儲存設定選項</param>
    public void Save(string path, OdfSaveOptions? options = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Package.SaveToStream(stream, options);
    }

    /// <summary>
    /// 非同步儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的 Task 執行個體</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與串流 I/O 期間協作檢查取消語彙。
    /// </remarks>
    public virtual async Task SaveAsync(OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        await Package.SaveAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 非同步將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與檔案 I/O 期間協作檢查取消語彙。
    /// </remarks>
    public async Task SaveAsync(string path, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await Package.SaveToStreamAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Web Streaming APIs

    /// <summary>
    /// 將文件儲存為 ODF 封裝位元組陣列。
    /// </summary>
    /// <returns>包含文件封裝內容的位元組陣列</returns>
    public byte[] SaveToBytes()
    {
        using var ms = new MemoryStream();
        SaveToStream(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options = null)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        Package.SaveToStream(destinationStream, options);

        if (destinationStream.CanSeek)
        {
            destinationStream.Position = 0;
        }
    }

    /// <summary>
    /// 非同步將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    public Task SaveAsync(Stream destinationStream, OdfSaveOptions? options = null, CancellationToken cancellationToken = default) =>
        SaveToStreamAsync(destinationStream, options, cancellationToken);

    /// <summary>
    /// 非同步將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與串流 I/O 期間協作檢查取消語彙。
    /// </remarks>
    public async Task SaveToStreamAsync(Stream destinationStream, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (destinationStream is null)
            throw new ArgumentNullException(nameof(destinationStream));

        options ??= OdfSaveOptions.Default;
        OdfDocumentPersistenceEngine.PrepareDomEntriesForSave(PersistenceCollaborators, options);
        await Package.SaveToStreamAsync(destinationStream, options, cancellationToken).ConfigureAwait(false);

        if (destinationStream.CanSeek)
            destinationStream.Position = 0;
    }

    #endregion

    #region Template Instantiation API

    /// <summary>
    /// 從指定的範本建立文件封裝的基礎實作。
    /// </summary>
    /// <param name="template">範本文件</param>
    /// <param name="targetKind">目標文件種類</param>
    /// <param name="targetMimeType">目標 MIME 媒體類型</param>
    /// <param name="clearUserContent">
    /// 是否清除範本中的使用者內容（例如文字文件的段落、試算表的資料列），但保留格式與版面配置
    /// （例如樣式、母片頁面、欄寬）。預設為 <see langword="false"/>，與既有完整複製行為相同。
    /// </param>
    internal static OdfDocument CreateFromTemplateInternal(
        OdfDocument template,
        OdfDocumentKind targetKind,
        string targetMimeType,
        bool clearUserContent = false)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        byte[] packageBytes = template.SaveToBytes();
        var ms = new MemoryStream(packageBytes);
        OdfPackage package = OdfPackage.Open(ms);

        package.SetMimeType(targetMimeType);
        package.Version = OdfVersion.Odf14;

        OdfDocument newDoc = OdfDocumentFactory.CreateDocumentWrapper(package, targetKind);

        if (newDoc.ContentDom != null)
            newDoc.ContentDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newDoc.StylesDom != null)
            newDoc.StylesDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newDoc.MetaDom != null)
            newDoc.MetaDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newDoc.SettingsDom != null)
            newDoc.SettingsDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");

        newDoc.Creator = null;
        newDoc.CreationDate = DateTime.UtcNow;
        newDoc.ModificationDate = DateTime.UtcNow;
        newDoc.TemplateMetadata = null;

        if (clearUserContent)
            newDoc.ClearTemplateUserContent();

        return newDoc;
    }

    /// <summary>
    /// 從指定的一般文件建立對應範本文件封裝的基礎實作（與 <see cref="CreateFromTemplateInternal"/> 方向相反）。
    /// </summary>
    /// <param name="document">要轉換為範本的來源文件</param>
    /// <param name="targetTemplateKind">目標範本文件種類</param>
    /// <param name="targetTemplateMimeType">目標範本 MIME 媒體類型</param>
    /// <remarks>
    /// 與 <see cref="CreateFromTemplateInternal"/> 不同，此方法預設完整保留來源文件的內容，
    /// 因為「將現有文件另存為範本」的常見使用情境是保留既有內容供未來重複使用，而非清空內容。
    /// </remarks>
    internal static OdfDocument CreateTemplateFromDocumentInternal(
        OdfDocument document,
        OdfDocumentKind targetTemplateKind,
        string targetTemplateMimeType)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        byte[] packageBytes = document.SaveToBytes();
        var ms = new MemoryStream(packageBytes);
        OdfPackage package = OdfPackage.Open(ms);

        package.SetMimeType(targetTemplateMimeType);
        package.Version = OdfVersion.Odf14;

        OdfDocument newTemplate = OdfDocumentFactory.CreateDocumentWrapper(package, targetTemplateKind);

        if (newTemplate.ContentDom != null)
            newTemplate.ContentDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newTemplate.StylesDom != null)
            newTemplate.StylesDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newTemplate.MetaDom != null)
            newTemplate.MetaDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");
        if (newTemplate.SettingsDom != null)
            newTemplate.SettingsDom.SetAttribute("version", OdfNamespaces.Office, "1.4", "office");

        return newTemplate;
    }

    /// <summary>
    /// 清除範本實例化後的使用者內容，但保留格式與版面配置。
    /// </summary>
    /// <remarks>
    /// 基底實作不做任何事；各文件種類（文字、試算表、簡報、繪圖）於對應的部分類別中覆寫，
    /// 依各自的內容模型清除使用者資料。
    /// </remarks>
    protected virtual void ClearTemplateUserContent()
    {
    }

    /// <summary>
    /// 遞迴清除指定節點底下所有 <c>text:p</c>／<c>text:span</c> 段落的文字內容，但保留節點結構
    /// （例如形狀、框架），用於簡報與繪圖範本清除使用者文字內容但保留版面配置。
    /// </summary>
    /// <param name="node">要清除文字內容的根節點</param>
    protected static void ClearParagraphTextContentRecursive(OdfNode node)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "p" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                foreach (OdfNode grandchild in new List<OdfNode>(child.Children))
                {
                    child.RemoveChild(grandchild);
                }
                child.TextContent = string.Empty;
                continue;
            }

            ClearParagraphTextContentRecursive(child);
        }
    }

    #endregion

    #region Flat XML Conversion APIs

    /// <summary>
    /// 將文件儲存為單一 Flat XML 格式的檔案。
    /// </summary>
    /// <param name="path">要儲存的檔案路徑</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public void SaveAsFlatXml(string path, OdfSaveOptions? options = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        bool originalFlat = Package.IsFlatXml;
        try
        {
            Package.IsFlatXml = true;
            Save(path, options);
        }
        finally
        {
            Package.IsFlatXml = originalFlat;
        }
    }

    /// <summary>
    /// 將文件儲存為單一 Flat XML 格式並寫入指定的資料流。
    /// </summary>
    /// <param name="stream">目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public void SaveAsFlatXml(Stream stream, OdfSaveOptions? options = null)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        options ??= OdfSaveOptions.Default;
        bool originalFlat = Package.IsFlatXml;
        try
        {
            Package.IsFlatXml = true;
            SaveToStream(stream, options);
        }
        finally
        {
            Package.IsFlatXml = originalFlat;
        }
    }

    /// <summary>
    /// 從指定的 Flat XML 檔案載入 ODF 文件。
    /// </summary>
    /// <param name="path">Flat XML 檔案路徑</param>
    /// <param name="options">載入選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>載入完成的 ODF 文件</returns>
    public static OdfDocument LoadFromFlatXml(string path, OdfLoadOptions? options = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var doc = OdfDocumentFactory.LoadDocument(path, options);
        doc.Package.IsFlatXml = true;
        return doc;
    }

    /// <summary>
    /// 從指定的 Flat XML 資料流載入 ODF 文件。
    /// </summary>
    /// <param name="stream">Flat XML 資料流</param>
    /// <param name="options">載入選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>載入完成 of ODF 文件</returns>
    public static OdfDocument LoadFromFlatXml(Stream stream, OdfLoadOptions? options = null)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        var doc = OdfDocumentFactory.LoadDocument(stream, options);
        doc.Package.IsFlatXml = true;
        return doc;
    }

    /// <summary>
    /// 在記憶體中將文件轉換為指定文件種類，並設定目標的 Flat XML／ZIP 封裝形態，
    /// 為四主格式 <c>CreateFromFlatDocument</c>／<c>CreateFromDocument</c> 型別化
    /// Flat↔ZIP 雙向轉換 API 的共用基礎實作。
    /// </summary>
    /// <param name="document">來源文件（可為 Flat XML 或 ZIP 封裝形態）</param>
    /// <param name="targetKind">目標 <see cref="OdfDocumentKind"/></param>
    /// <param name="targetIsFlatXml">轉換結果是否應為 Flat XML 形態</param>
    /// <returns>轉換完成的文件，封裝內容與來源完全相同，僅 Flat XML／ZIP 形態與種類標記不同</returns>
    internal static OdfDocument ConvertFlatVariantInternal(
        OdfDocument document,
        OdfDocumentKind targetKind,
        bool targetIsFlatXml)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        byte[] packageBytes = document.SaveToBytes();
        var ms = new MemoryStream(packageBytes);
        OdfPackage package = OdfPackage.Open(ms);
        package.IsFlatXml = targetIsFlatXml;

        return OdfDocumentFactory.CreateDocumentWrapper(package, targetKind);
    }

    /// <summary>
    /// 將一般 ZIP 封裝的 ODF 文件就地轉換為 Flat XML 格式。
    /// </summary>
    /// <param name="sourcePath">來源 ZIP 封裝文件路徑</param>
    /// <param name="destinationPath">目標 Flat XML 文件路徑</param>
    /// <param name="loadOptions">載入選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="saveOptions">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public static void ConvertZipToFlatXml(
        string sourcePath,
        string destinationPath,
        OdfLoadOptions? loadOptions = null,
        OdfSaveOptions? saveOptions = null)
    {
        if (sourcePath is null)
            throw new ArgumentNullException(nameof(sourcePath));
        if (destinationPath is null)
            throw new ArgumentNullException(nameof(destinationPath));

        using OdfDocument doc = OdfDocumentFactory.LoadDocument(sourcePath, loadOptions);
        doc.SaveAsFlatXml(destinationPath, saveOptions);
    }

    /// <summary>
    /// 將 Flat XML 格式的 ODF 文件就地轉換為一般 ZIP 封裝格式。
    /// </summary>
    /// <param name="sourcePath">來源 Flat XML 文件路徑</param>
    /// <param name="destinationPath">目標 ZIP 封裝文件路徑</param>
    /// <param name="loadOptions">載入選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="saveOptions">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public static void ConvertFlatXmlToZip(
        string sourcePath,
        string destinationPath,
        OdfLoadOptions? loadOptions = null,
        OdfSaveOptions? saveOptions = null)
    {
        if (sourcePath is null)
            throw new ArgumentNullException(nameof(sourcePath));
        if (destinationPath is null)
            throw new ArgumentNullException(nameof(destinationPath));

        using OdfDocument doc = LoadFromFlatXml(sourcePath, loadOptions);
        doc.Package.IsFlatXml = false;
        doc.Save(destinationPath, saveOptions);
    }

    #endregion
}
