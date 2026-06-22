#pragma warning restore CS1591

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// 代表一個 ODF 文件封裝容器的抽象基底類別。
/// </summary>
public abstract partial class OdfDocument : IDisposable, IAsyncDisposable
{
    private const string DocumentSignaturesPath = "META-INF/documentsignatures.xml";

    /// <summary>
    /// 建立指定種類的 ODF 文件。
    /// </summary>
    /// <param name="kind">要建立的 ODF 文件種類</param>
    /// <returns>建立完成的 ODF 文件</returns>
    public static OdfDocument Create(OdfDocumentKind kind) => OdfDocumentFactory.CreateDocument(kind);

    /// <summary>
    /// 從指定路徑載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑</param>
    /// <returns>載入完成的 ODF 文件</returns>
    /// <remarks>在 ASP.NET Core 等伺服器環境中，請優先使用 <see cref="LoadAsync(string, CancellationToken)"/> 以避免阻塞執行緒</remarks>
    public static OdfDocument Load(string path) => OdfDocumentFactory.LoadDocument(path);

    /// <summary>
    /// 非同步從指定路徑載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 ODF 文件</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken);

    /// <summary>
    /// 從指定路徑與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制</param>
    /// <returns>載入完成的 ODF 文件</returns>
    public static OdfDocument Load(string path, OdfLoadOptions? options) => OdfDocumentFactory.LoadDocument(path, options);

    /// <summary>
    /// 非同步從指定路徑與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 ODF 文件</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfDocument> LoadAsync(string path, OdfLoadOptions? options, CancellationToken cancellationToken = default) =>
        OdfDocumentFactory.LoadDocumentAsync(path, options, cancellationToken);

    /// <summary>
    /// 從指定資料流載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 ODF 文件</returns>
    public static OdfDocument Load(Stream stream, string? fileName = null) => OdfDocumentFactory.LoadDocument(stream, fileName);

    /// <summary>
    /// 非同步從指定資料流載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 ODF 文件</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public static Task<OdfDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken);

    /// <summary>
    /// 從指定資料流與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 ODF 文件</returns>
    public static OdfDocument Load(Stream stream, OdfLoadOptions? options, string? fileName = null) =>
        OdfDocumentFactory.LoadDocument(stream, options, fileName);

    /// <summary>
    /// 非同步從指定資料流與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 ODF 文件</returns>
    public static Task<OdfDocument> LoadAsync(Stream stream, OdfLoadOptions? options, string? fileName = null, CancellationToken cancellationToken = default) =>
        OdfDocumentFactory.LoadDocumentAsync(stream, options, fileName, cancellationToken);

    /// <summary>
    /// 取得與此文件相關聯的 ODF 封裝容器。
    /// </summary>
    public OdfPackage Package { get; }

    /// <summary>
    /// 取得目前封裝 MIME 類型所宣告的 ODF 文件種類。
    /// </summary>
    public OdfDocumentKind DocumentKind
    {
        get
        {
            OdfDocumentKind mimeKind = OdfDocumentKindDetector.FromMimeType(Package.MimeType);
            return Package.IsFlatXml && mimeKind != OdfDocumentKind.Unknown
                ? OdfDocumentKindDetector.ToFlatKind(mimeKind)
                : mimeKind;
        }
    }

    /// <summary>
    /// 取得目前文件種類對應的格式描述；若 MIME 類型無法辨識則為 <see langword="null"/>。
    /// </summary>
    public OdfFormatInfo? Format => OdfDocumentKindDetector.TryGetFormatByKind(DocumentKind, out OdfFormatInfo? format)
        ? format
        : null;

    /// <summary>
    /// 取得目前文件在 <c>office:body</c> 下使用的內容種類。
    /// </summary>
    public OdfDocumentKind ContentKind => OdfDocumentKindDetector.ToContentKind(DocumentKind);

    /// <summary>
    /// 取得一個值，指出目前文件是否為 ODF 範本格式。
    /// </summary>
    public bool IsTemplate => OdfDocumentKindDetector.IsTemplateKind(DocumentKind);

    /// <summary>
    /// 取得一個值，指出目前文件是否為 ODF 主控文件格式。
    /// </summary>
    public bool IsMasterDocument => OdfDocumentKindDetector.IsMasterKind(DocumentKind);

    /// <summary>
    /// 取得一個值，指出目前文件是否為單一 XML (Flat XML) ODF 格式。
    /// </summary>
    public bool IsFlatXml => Package.IsFlatXml;

    /// <summary>
    /// 取得或設定文件儲存時的目標 ODF 版本。
    /// 若為 <see langword="null"/>（預設），則沿用現有 DOM 中的版本宣告。
    /// 設定後，存檔時會覆寫 <c>office:version</c> 及 manifest 中的版本字串。
    /// </summary>
    public OdfVersion? TargetVersion { get; set; }

    /// <summary>
    /// 取得或設定文件的內容 DOM 樹。
    /// </summary>
    internal OdfNode ContentDom { get; private protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的樣式 DOM 樹。
    /// </summary>
    internal OdfNode StylesDom { get; private protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的樣式引擎。
    /// </summary>
    internal OdfStyleEngine StyleEngine { get; private protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的中繼資料 DOM 樹。
    /// </summary>
    internal OdfNode MetaDom { get; private protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的設定值 DOM 樹。
    /// </summary>
    internal OdfNode SettingsDom { get; private protected set; } = null!;

    /// <summary>
    /// 取得或設定文件內容的根節點。
    /// </summary>
    internal OdfNode ContentRoot { get => ContentDom; private protected set => ContentDom = value; }

    /// <summary>
    /// 取得或設定文件樣式的根節點。
    /// </summary>
    internal OdfNode StylesRoot { get => StylesDom; private protected set => StylesDom = value; }

    /// <summary>
    /// 取得或設定文件中繼資料的根節點。
    /// </summary>
    internal OdfNode MetaRoot { get => MetaDom; private protected set => MetaDom = value; }

    /// <summary>
    /// 取得或設定文件設定值的根節點。
    /// </summary>
    internal OdfNode SettingsRoot { get => SettingsDom; private protected set => SettingsDom = value; }

    private bool _isDisposed;

    /// <summary>
    /// 取得或設定嵌入式文件在封裝容器內的路徑。若為根文件則為空字串。
    /// </summary>
    public string SubPath { get; set; } = string.Empty;

    /// <summary>
    /// 初始化 <see cref="OdfDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">OdfPackage 封裝容器</param>
    protected OdfDocument(OdfPackage package) : this(package, string.Empty)
    {
    }

    /// <summary>
    /// 初始化指定子路徑的 <see cref="OdfDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">OdfPackage 封裝容器</param>
    /// <param name="subPath">嵌入式文件在封裝中的子路徑</param>
    protected OdfDocument(OdfPackage package, string subPath)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        SubPath = subPath ?? string.Empty;
        if (!string.IsNullOrEmpty(SubPath) && !SubPath.EndsWith("/"))
        {
            SubPath += "/";
        }
        LoadXmlTrees();
        OdfLoExtInteropEngine.NormalizeLoadedDocument(ContentDom, StylesDom);

        // 載入與正規化期間，XML 剖析器與正規化邏輯本身會將所有節點標記為已修改；
        // 在此重設為乾淨基準狀態，讓 OdfStyleEngine 的 Dirty 旗標檢查只反映載入後的使用者編輯。
        ContentDom.ResetModifiedState();
        StylesDom.ResetModifiedState();

        StyleEngine = new OdfStyleEngine(ContentDom, StylesDom);
    }

    /// <summary>
    /// 清理文件，移除所有 VBA 與 StarBasic 巨集指令碼、數位簽章及指令碼參照。
    /// </summary>
    public void SanitizeMacros()
    {
        OdfPackage.SanitizeXmlNode(ContentDom);
        OdfPackage.SanitizeXmlNode(StylesDom);
        OdfPackage.SanitizeXmlNode(MetaDom);
        OdfPackage.SanitizeXmlNode(SettingsDom);
        Package.SanitizeMacros();
    }

    private void LoadXmlTrees()
    {
        ContentDom = LoadOrInitDom("content.xml", GetDefaultContentXml());
        StylesDom = LoadOrInitDom("styles.xml", GetDefaultStylesXml());
        MetaDom = LoadOrInitDom("meta.xml", GetDefaultMetaXml());
        SettingsDom = LoadOrInitDom("settings.xml", GetDefaultSettingsXml());
    }

    private OdfNode LoadOrInitDom(string entryName, string defaultXml)
    {
        string path = string.IsNullOrEmpty(SubPath) ? entryName : SubPath + entryName;
        if (Package.HasEntry(path))
        {
            using var stream = Package.GetEntryStream(path);
            return OdfXmlReader.Parse(stream, Package.LoadOptions);
        }
        else
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(defaultXml));
            return OdfXmlReader.Parse(ms, Package.LoadOptions);
        }
    }

    /// <summary>
    /// 取得此文件類型的預設 content.xml。
    /// </summary>
    /// <returns>預設 content.xml 字串</returns>
    protected abstract string GetDefaultContentXml();

    /// <summary>
    /// 取得儲存至封裝容器時，<c>content.xml</c> 實際應寫入的根節點。
    /// 預設直接沿用 <see cref="ContentDom"/>；少數文件類型（例如 ODF 公式文件）的封裝格式
    /// 與 OdfKit 內部慣用的 <c>office:document-content</c> 包裹結構不同，可覆寫此方法轉換輸出形狀。
    /// </summary>
    /// <returns>實際應序列化寫入 <c>content.xml</c> 的根節點</returns>
    internal virtual OdfNode GetContentXmlForPersistence() => ContentDom;

    /// <summary>
    /// 取得此文件類型的預設 styles.xml。
    /// </summary>
    /// <returns>預設 styles.xml 字串</returns>
    protected abstract string GetDefaultStylesXml();

    /// <summary>
    /// 取得此文件類型的預設 meta.xml。
    /// </summary>
    /// <returns>預設 meta.xml 字串</returns>
    protected virtual string GetDefaultMetaXml()
    {
        return "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:meta></office:meta></office:document-meta>";
    }

    /// <summary>
    /// 取得此文件類型的預設 settings.xml。
    /// </summary>
    /// <returns>預設 settings.xml 字串</returns>
    protected virtual string GetDefaultSettingsXml()
    {
        return "<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:settings><config:config-item-set config:name=\"ooo:view-settings\"><config:config-item config:name=\"VisibleAreaTop\" config:type=\"int\">0</config:config-item></config:config-item-set></office:settings></office:document-settings>";
    }


    /// <summary>
    /// 釋放文件與底層封裝資源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 釋放文件持有的資源。
    /// </summary>
    /// <param name="disposing">若為 <see langword="true"/>，則釋放受控資源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                Package.Dispose();
            }
            _isDisposed = true;
        }
    }

    /// <summary>
    /// 非同步釋放文件與底層封裝資源。
    /// </summary>
    /// <returns>代表非同步釋放作業的值工作</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            await Package.DisposeAsync().ConfigureAwait(false);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Zoom & View Settings (settings.xml)


    /// <summary>
    /// 取得或設定文件檢視縮放百分比。
    /// </summary>
    public double ZoomLevel
    {
        get => OdfDocumentSettingsEngine.GetZoomLevel(SettingsDom);
        set => OdfDocumentSettingsEngine.SetZoomLevel(SettingsDom, value);
    }


    #endregion

}
