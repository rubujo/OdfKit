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
public abstract class OdfDocument : IDisposable, IAsyncDisposable
{
    private const string DocumentSignaturesPath = "META-INF/documentsignatures.xml";

    /// <summary>
    /// 建立指定種類的 ODF 文件。
    /// </summary>
    /// <param name="kind">要建立的 ODF 文件種類。</param>
    /// <returns>建立完成的 ODF 文件。</returns>
    public static OdfDocument Create(OdfDocumentKind kind) => OdfDocumentFactory.CreateDocument(kind);

    /// <summary>
    /// 從指定路徑載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument Load(string path) => OdfDocumentFactory.LoadDocument(path);

    /// <summary>
    /// 從指定路徑與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑。</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument Load(string path, OdfLoadOptions? options) => OdfDocumentFactory.LoadDocument(path, options);

    /// <summary>
    /// 從指定資料流載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument Load(Stream stream, string? fileName = null) => OdfDocumentFactory.LoadDocument(stream, fileName);

    /// <summary>
    /// 從指定資料流與載入選項載入 ODF 文件。
    /// </summary>
    /// <param name="stream">包含 ODF 文件內容的資料流。</param>
    /// <param name="options">載入選項，例如加密文件密碼與安全限制。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 ODF 文件。</returns>
    public static OdfDocument Load(Stream stream, OdfLoadOptions? options, string? fileName = null) =>
        OdfDocumentFactory.LoadDocument(stream, options, fileName);

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
    /// 取得或設定文件的內容 DOM 樹。
    /// </summary>
    public OdfNode ContentDom { get; protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的樣式 DOM 樹。
    /// </summary>
    public OdfNode StylesDom { get; protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的樣式引擎。
    /// </summary>
    public OdfStyleEngine StyleEngine { get; protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的中繼資料 DOM 樹。
    /// </summary>
    public OdfNode MetaDom { get; protected set; } = null!;

    /// <summary>
    /// 取得或設定文件的設定值 DOM 樹。
    /// </summary>
    public OdfNode SettingsDom { get; protected set; } = null!;

    /// <summary>
    /// 取得或設定文件內容的根節點。
    /// </summary>
    public OdfNode ContentRoot { get => ContentDom; protected set => ContentDom = value; }

    /// <summary>
    /// 取得或設定文件樣式的根節點。
    /// </summary>
    public OdfNode StylesRoot { get => StylesDom; protected set => StylesDom = value; }

    /// <summary>
    /// 取得或設定文件中繼資料的根節點。
    /// </summary>
    public OdfNode MetaRoot { get => MetaDom; protected set => MetaDom = value; }

    /// <summary>
    /// 取得或設定文件設定值的根節點。
    /// </summary>
    public OdfNode SettingsRoot { get => SettingsDom; protected set => SettingsDom = value; }

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
        /// <returns>預設 content.xml 字串。</returns>
        protected abstract string GetDefaultContentXml();

        /// <summary>
        /// 取得此文件類型的預設 styles.xml。
        /// </summary>
        /// <returns>預設 styles.xml 字串。</returns>
        protected abstract string GetDefaultStylesXml();

        /// <summary>
        /// 取得此文件類型的預設 meta.xml。
        /// </summary>
        /// <returns>預設 meta.xml 字串。</returns>
        protected virtual string GetDefaultMetaXml()
        {
            return "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:meta></office:meta></office:document-meta>";
        }

        /// <summary>
        /// 取得此文件類型的預設 settings.xml。
        /// </summary>
        /// <returns>預設 settings.xml 字串。</returns>
        protected virtual string GetDefaultSettingsXml()
        {
            return "<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:settings><config:config-item-set config:name=\"ooo:view-settings\"><config:config-item config:name=\"VisibleAreaTop\" config:type=\"int\">0</config:config-item></config:config-item-set></office:settings></office:document-settings>";
        }

        #region Package Lifecycle & Persistence

    /// <summary>
    /// 儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    public virtual void Save(OdfSaveOptions? options = null)
    {
        options ??= OdfSaveOptions.Default;

        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        Package.Save(options);
    }

    /// <summary>
    /// 將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑。</param>
    /// <param name="options">儲存設定選項。</param>
    public void Save(string path, OdfSaveOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Package.SaveToStream(stream, options);
    }

    /// <summary>
    /// 非同步儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的 Task 執行個體</returns>
    public virtual async Task SaveAsync(OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OdfSaveOptions.Default;

        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        await Package.SaveAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private void WriteDomToEntry(string name, OdfNode node, OdfSaveOptions options)
    {
        using var ms = new MemoryStream();
        OdfXmlWriter.Write(node, ms, options);
        string path = string.IsNullOrEmpty(SubPath) ? name : SubPath + name;
        Package.WriteEntry(path, ms.ToArray(), "text/xml");
    }

    #endregion

    #region High-Level Digital Signatures

    /// <summary>
    /// 使用指定的 X.509 憑證對文件進行數位簽章。
    /// </summary>
    /// <param name="certificate">用於簽章的憑證</param>
    public void Sign(X509Certificate2 certificate)
    {
        StyleEngine.DeduplicateAndSaveStyles();
        WriteDomToEntry("content.xml", ContentDom, OdfSaveOptions.Default);
        WriteDomToEntry("styles.xml", StylesDom, OdfSaveOptions.Default);
        WriteDomToEntry("meta.xml", MetaDom, OdfSaveOptions.Default);
        WriteDomToEntry("settings.xml", SettingsDom, OdfSaveOptions.Default);

        OdfSigner.Sign(Package, certificate);
    }

    /// <summary>
    /// 取得文件封裝內數位簽章項目的摘要狀態。
    /// </summary>
    /// <returns>描述簽章項目存在狀態、可讀性與簽章數量的摘要。</returns>
    public OdfDocumentSignatureSummary GetSignatureSummary()
    {
        if (!Package.HasEntry(DocumentSignaturesPath))
        {
            return OdfDocumentSignatureSummary.Unsigned(DocumentSignaturesPath);
        }

        try
        {
            using Stream stream = Package.GetEntryStream(DocumentSignaturesPath);
            int signatureCount = CountSignatureElements(stream, Package.LoadOptions.MaxXmlCharactersInDocument);
            return OdfDocumentSignatureSummary.Readable(DocumentSignaturesPath, signatureCount);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is XmlException)
        {
            return OdfDocumentSignatureSummary.Unreadable(DocumentSignaturesPath, ex.Message);
        }
    }

    /// <summary>
    /// 驗證文件中的所有數位簽章。
    /// </summary>
    /// <param name="certificates">輸出參數，傳回驗證通過的憑證集合</param>
    /// <returns>若所有簽章皆驗證成功則傳回 true；否則傳回 false</returns>
    public bool VerifySignatures(out X509Certificate2Collection certificates)
    {
        return OdfSigner.VerifySignatures(Package, out certificates);
    }

    /// <summary>
    /// 驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <returns>詳細的數位簽章驗證結果。</returns>
    public OdfSignatureValidationResult VerifySignatures(OdfSigningOptions? options = null)
    {
        return OdfSigner.VerifySignatures(Package, options);
    }

    /// <summary>
    /// 非同步驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果。</returns>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(OdfSigningOptions? options = null)
    {
        return OdfSigner.VerifySignaturesAsync(Package, options);
    }

    private static int CountSignatureElements(Stream stream, long maxCharsInDocument = 0)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharsInDocument > 0 ? maxCharsInDocument : 0
        };

        int count = 0;
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "Signature" &&
                reader.NamespaceURI == OdfNamespaces.Ds)
            {
                count++;
            }
        }

        return count;
    }

        #endregion

        #region Metadata API (meta.xml)

        /// <summary>
        /// 取得或設定文件標題。
        /// </summary>
        public string? Title
        {
            get => GetMetaElementText("dc:title");
            set => SetMetaElementText("dc:title", value);
        }

        /// <summary>
        /// 取得或設定文件建立者。
        /// </summary>
        public string? Creator
        {
            get => GetMetaElementText("dc:creator");
            set => SetMetaElementText("dc:creator", value);
        }

        /// <summary>
        /// 取得或設定文件描述。
        /// </summary>
        public string? Description
        {
            get => GetMetaElementText("dc:description");
            set => SetMetaElementText("dc:description", value);
        }

        /// <summary>
        /// 取得或設定文件主旨。
        /// </summary>
        public string? Subject
        {
            get => GetMetaElementText("dc:subject");
            set => SetMetaElementText("dc:subject", value);
        }

        /// <summary>
        /// 取得或設定文件語言。
        /// </summary>
        public string? Language
        {
            get => GetMetaElementText("dc:language");
            set => SetMetaElementText("dc:language", value);
        }

        /// <summary>
        /// 取得或設定文件建立日期。
        /// </summary>
        public DateTime? CreationDate
        {
            get => ParseMetaDate(GetMetaElementText("meta:creation-date"));
            set => SetMetaElementText("meta:creation-date", FormatMetaDate(value));
        }

        /// <summary>
        /// 取得或設定文件修改日期。
        /// </summary>
        public DateTime? ModificationDate
        {
            get => ParseMetaDate(GetMetaElementText("dc:date"));
            set => SetMetaElementText("dc:date", FormatMetaDate(value));
        }

        /// <summary>
        /// 設定自訂中繼資料屬性。
        /// </summary>
        /// <param name="name">屬性名稱。</param>
        /// <param name="value">屬性值。</param>
        /// <param name="type">ODF 中繼資料值類型，例如 string、float、boolean 或 date。</param>
        public void SetCustomProperty(string name, object value, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property name cannot be empty.", nameof(name));
            
            if (name.Contains(":"))
            {
                string oldName = name;
                name = name.Replace(":", "_");
                OdfKitDiagnostics.Warn($"Custom property name '{oldName}' contains invalid character ':'. Renamed to '{name}' for Excel compatibility.");
            }

            var metaRoot = FindOrCreateMetaRoot();
            
            OdfNode? existing = FindCustomPropertyNode(metaRoot, name);
            if (existing != null) metaRoot.RemoveChild(existing);

            var propNode = new OdfNode(OdfNodeType.Element, "user-defined", OdfNamespaces.Meta, "meta");
            propNode.SetAttribute("name", OdfNamespaces.Meta, name, "meta");
            propNode.SetAttribute("value-type", OdfNamespaces.Meta, type, "meta");
            propNode.TextContent = FormatValue(value, type);

            metaRoot.AppendChild(propNode);
        }

        /// <summary>
        /// 取得自訂中繼資料屬性。
        /// </summary>
        /// <param name="name">屬性名稱。</param>
        /// <returns>屬性值；若不存在則為 <see langword="null"/>。</returns>
        public object? GetCustomProperty(string name)
        {
            var metaRoot = FindOrCreateMetaRoot();
            var propNode = FindCustomPropertyNode(metaRoot, name);
            if (propNode == null) return null;

            string? type = propNode.GetAttribute("value-type", OdfNamespaces.Meta);
            string valStr = propNode.TextContent;
            return ParseValue(valStr, type);
        }

        #endregion

        #region Zoom & View Settings (settings.xml)

        /// <summary>
        /// 取得或設定文件檢視縮放百分比。
        /// </summary>
        public double ZoomLevel
        {
            get => GetZoomLevelInternal();
            set => SetZoomLevelInternal(value);
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
            if (destinationStream == null) throw new ArgumentNullException(nameof(destinationStream));

            options ??= OdfSaveOptions.Default;
            StyleEngine.DeduplicateAndSaveStyles();
            UpdateDocumentStatistics();
            ApplySaveVersionOptions(options);

            WriteDomToEntry("content.xml", ContentDom, options);
            WriteDomToEntry("styles.xml", StylesDom, options);
            WriteDomToEntry("meta.xml", MetaDom, options);
            WriteDomToEntry("settings.xml", SettingsDom, options);

            Package.SaveToStream(destinationStream, options);
            
            if (destinationStream.CanSeek)
            {
                destinationStream.Position = 0;
            }
        }

        #endregion

        #region Document Merging API

        /// <summary>
        /// 將另一份 ODF 文件附加到目前文件。
        /// </summary>
        /// <param name="otherDoc">要附加的來源文件。</param>
        /// <param name="options">合併選項。</param>
        public virtual void AppendDocument(OdfDocument otherDoc, OdfMergeOptions? options = null)
        {
            options ??= OdfMergeOptions.Default;
            if (otherDoc == null) throw new ArgumentNullException(nameof(otherDoc));

            var styleRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

            if (options.ImportStyles)
            {
                MergeStyles(otherDoc, options, styleRenameMap);
            }

            MergeContentNodes(otherDoc, options, styleRenameMap);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 尋找或建立 office:meta 根節點。
        /// </summary>
        /// <returns>office:meta 節點。</returns>
        protected OdfNode FindOrCreateMetaRoot()
        {
            foreach (var child in MetaDom.Children)
            {
                if (child.LocalName == "meta" && child.NamespaceUri == OdfNamespaces.Office)
                    return child;
            }
            var root = new OdfNode(OdfNodeType.Element, "meta", OdfNamespaces.Office, "office");
            MetaDom.AppendChild(root);
            return root;
        }

        private OdfNode? FindCustomPropertyNode(OdfNode metaRoot, string name)
        {
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == "user-defined" && 
                    child.NamespaceUri == OdfNamespaces.Meta && 
                    child.GetAttribute("name", OdfNamespaces.Meta) == name)
                {
                    return child;
                }
            }
            return null;
        }

        private string? GetMetaElementText(string qualifiedName)
        {
            var metaRoot = FindOrCreateMetaRoot();
            string localName = qualifiedName.Split(':')[1];
            string ns = qualifiedName.StartsWith("dc:") ? OdfNamespaces.Dc : OdfNamespaces.Meta;

            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child.TextContent;
            }
            return null;
        }

        private void SetMetaElementText(string qualifiedName, string? value)
        {
            var metaRoot = FindOrCreateMetaRoot();
            string[] parts = qualifiedName.Split(':');
            string localName = parts[1];
            string ns = parts[0] == "dc" ? OdfNamespaces.Dc : OdfNamespaces.Meta;
            string prefix = parts[0];

            OdfNode? target = null;
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                {
                    target = child;
                    break;
                }
            }

            if (value == null)
            {
                if (target != null) metaRoot.RemoveChild(target);
            }
            else
            {
                if (target == null)
                {
                    target = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
                    metaRoot.AppendChild(target);
                }
                target.TextContent = value;
            }
        }

        private DateTime? ParseMetaDate(string? text)
        {
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var val))
            {
                if (val == DateTime.MinValue || val == DateTime.MaxValue)
                    return val;
                try
                {
                    return val.ToUniversalTime();
                }
                catch (ArgumentOutOfRangeException)
                {
                    return val;
                }
            }
            return null;
        }

        private string? FormatMetaDate(DateTime? dt)
        {
            if (dt == null) return null;
            var val = dt.Value;
            if (val == DateTime.MinValue || val == DateTime.MaxValue)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            try
            {
                return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private string FormatValue(object val, string type)
        {
            return type.ToLowerInvariant() switch
            {
                "boolean" => ((bool)val) ? "true" : "false",
                "float" => Convert.ToDouble(val).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "date" => FormatDateValue((DateTime)val),
                _ => val.ToString() ?? string.Empty
            };
        }

        private string FormatDateValue(DateTime val)
        {
            if (val == DateTime.MinValue || val == DateTime.MaxValue)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            try
            {
                return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private object ParseValue(string val, string? type)
        {
            return type?.ToLowerInvariant() switch
            {
                "boolean" => bool.TryParse(val, out var b) && b,
                "float" => double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0.0,
                "date" => DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue,
                _ => val
            };
        }

        private double GetZoomLevelInternal()
        {
            var entry = FindSettingsConfigItem("ZoomValue");
            if (entry != null && double.TryParse(entry.TextContent, out var val))
                return val;
            return 100.0;
        }

        private void SetZoomLevelInternal(double zoom)
        {
            var settingsRoot = SettingsDom;
            var setNode = FindOrCreateSettingsNode(settingsRoot, "view-settings");
            var mapNode = FindOrCreateMapNode(setNode, "Views");
            var entryNode = FindOrCreateMapEntryNode(mapNode);
            var zoomNode = FindOrCreateConfigItemNode(entryNode, "ZoomValue", "int");
            zoomNode.TextContent = Math.Round(zoom).ToString();
            
            var zoomTypeNode = FindOrCreateConfigItemNode(entryNode, "ZoomType", "short");
            zoomTypeNode.TextContent = "0"; // 0: Direct Zoom percentage
        }

        /// <summary>
        /// 尋找指定名稱的設定項目。
        /// </summary>
        /// <param name="name">設定項目名稱。</param>
        /// <returns>設定項目節點；若不存在則為 <see langword="null"/>。</returns>
        protected OdfNode? FindSettingsConfigItem(string name)
        {
            return FindNodeByNameRecursive(SettingsDom, "config-item", name);
        }

        private OdfNode? FindNodeByNameRecursive(OdfNode parent, string localName, string nameAttr)
        {
            if (parent.LocalName == localName && parent.GetAttribute("name", OdfNamespaces.Config) == nameAttr)
                return parent;
            foreach (var child in parent.Children)
            {
                var f = FindNodeByNameRecursive(child, localName, nameAttr);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// 尋找或建立指定名稱的設定集合節點。
        /// </summary>
        /// <param name="root">設定 DOM 根節點。</param>
        /// <param name="name">設定集合名稱。</param>
        /// <returns>設定集合節點。</returns>
        protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
        {
            foreach (var child in root.Children)
            {
                if (child.LocalName == "settings" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    foreach (var sc in child.Children)
                    {
                        if (sc.LocalName == "config-item-set" && sc.GetAttribute("name", OdfNamespaces.Config) == name)
                            return sc;
                    }
                    var node = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
                    node.SetAttribute("name", OdfNamespaces.Config, name, "config");
                    child.AppendChild(node);
                    return node;
                }
            }
            var sets = new OdfNode(OdfNodeType.Element, "settings", OdfNamespaces.Office, "office");
            var setNode = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
            setNode.SetAttribute("name", OdfNamespaces.Config, name, "config");
            sets.AppendChild(setNode);
            root.AppendChild(sets);
            return setNode;
        }

        /// <summary>
        /// 尋找指定名稱的設定集合節點。
        /// </summary>
        /// <param name="root">設定 DOM 根節點。</param>
        /// <param name="name">設定集合名稱。</param>
        /// <returns>設定集合節點；若不存在則為 <see langword="null"/>。</returns>
        protected OdfNode? FindSettingsNode(OdfNode root, string name)
        {
            foreach (var child in root.Children)
            {
                if (child.LocalName == "settings" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    foreach (var sc in child.Children)
                    {
                        if (sc.LocalName == "config-item-set" && sc.GetAttribute("name", OdfNamespaces.Config) == name)
                            return sc;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 尋找或建立設定 map 節點。
        /// </summary>
        /// <param name="setNode">設定集合節點。</param>
        /// <param name="name">map 名稱。</param>
        /// <returns>設定 map 節點。</returns>
        protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
        {
            foreach (var child in setNode.Children)
            {
                if (child.LocalName == "config-item-map-indexed" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, "config-item-map-indexed", OdfNamespaces.Config, "config");
            node.SetAttribute("name", OdfNamespaces.Config, name, "config");
            setNode.AppendChild(node);
            return node;
        }

        /// <summary>
        /// 尋找或建立設定 map entry 節點。
        /// </summary>
        /// <param name="mapNode">設定 map 節點。</param>
        /// <returns>設定 map entry 節點。</returns>
        protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
        {
            if (mapNode.Children.Count > 0)
                return mapNode.Children[0];
            var node = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
            mapNode.AppendChild(node);
            return node;
        }

        /// <summary>
        /// 尋找或建立設定項目節點。
        /// </summary>
        /// <param name="entryNode">設定 map entry 節點。</param>
        /// <param name="name">設定項目名稱。</param>
        /// <param name="type">設定項目類型。</param>
        /// <returns>設定項目節點。</returns>
        protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
        {
            foreach (var child in entryNode.Children)
            {
                if (child.LocalName == "config-item" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
            node.SetAttribute("name", OdfNamespaces.Config, name, "config");
            node.SetAttribute("type", OdfNamespaces.Config, type, "config");
            entryNode.AppendChild(node);
            return node;
        }

        #endregion

        #region Statistics & Document Structure Diagnostics

        /// <summary>
        /// 更新文件統計中繼資料。
        /// </summary>
        protected virtual void UpdateDocumentStatistics()
        {
            int wordCount = 0;
            int charCount = 0;
            int paragraphCount = 0;
            int tableCount = 0;
            int imageCount = 0;

            TraverseForStats(ContentDom, ref wordCount, ref charCount, ref paragraphCount, ref tableCount, ref imageCount);

            var metaRoot = FindOrCreateMetaRoot();
            OdfNode? statNode = null;
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == "document-statistic" && child.NamespaceUri == OdfNamespaces.Meta)
                {
                    statNode = child;
                    break;
                }
            }

            if (statNode == null)
            {
                statNode = new OdfNode(OdfNodeType.Element, "document-statistic", OdfNamespaces.Meta, "meta");
                metaRoot.AppendChild(statNode);
            }

            statNode.SetAttribute("word-count", OdfNamespaces.Meta, wordCount.ToString(), "meta");
            statNode.SetAttribute("character-count", OdfNamespaces.Meta, charCount.ToString(), "meta");
            statNode.SetAttribute("paragraph-count", OdfNamespaces.Meta, paragraphCount.ToString(), "meta");
            statNode.SetAttribute("table-count", OdfNamespaces.Meta, tableCount.ToString(), "meta");
            statNode.SetAttribute("image-count", OdfNamespaces.Meta, imageCount.ToString(), "meta");
            statNode.SetAttribute("page-count", OdfNamespaces.Meta, "1", "meta"); // Layout engine placeholder
        }

        private void TraverseForStats(OdfNode node, ref int words, ref int chars, ref int paragraphs, ref int tables, ref int images)
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                chars += text.Length;
                
                string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                words += parts.Length;
                return;
            }

            if (node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text) paragraphs++;
            else if (node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table) tables++;
            else if (node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw) images++;

            foreach (var child in node.Children)
            {
                TraverseForStats(child, ref words, ref chars, ref paragraphs, ref tables, ref images);
            }
        }

        #endregion

        #region Internal Merging Helpers

        private void MergeStyleNodes(OdfNode sourceContainer, OdfNode destContainer, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            foreach (var srcStyle in sourceContainer.Children)
            {
                if (srcStyle.NodeType == OdfNodeType.Element && !string.IsNullOrEmpty(srcStyle.GetAttribute("name", OdfNamespaces.Style)))
                {
                    string name = srcStyle.GetAttribute("name", OdfNamespaces.Style)!;
                    string family = srcStyle.GetAttribute("family", OdfNamespaces.Style) ?? "paragraph";

                    bool conflict = StyleEngine.StyleExists(name);

                    if (conflict && options.StyleConflictResolution == ConflictResolution.KeepSourceFormatting)
                    {
                        string newName = GenerateUniqueStyleName(name, family);
                        renameMap[name] = newName;

                        var clonedStyle = srcStyle.CloneNode(true);
                        clonedStyle.SetAttribute("name", OdfNamespaces.Style, newName, "style");
                        destContainer.AppendChild(clonedStyle);
                    }
                    else if (!conflict)
                    {
                        var clonedStyle = srcStyle.CloneNode(true);
                        destContainer.AppendChild(clonedStyle);
                    }
                }
            }
        }

        private void MergeStyles(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var sourceContentAuto = FindOrCreateChild(sourceDoc.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
            var destContentAuto = FindOrCreateChild(ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceContentAuto, destContentAuto, options, renameMap);

            var sourceStylesStyles = FindOrCreateChild(sourceDoc.StylesDom, "styles", OdfNamespaces.Office, "office");
            var destStylesStyles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceStylesStyles, destStylesStyles, options, renameMap);

            var sourceStylesAuto = FindOrCreateChild(sourceDoc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
            var destStylesAuto = FindOrCreateChild(StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceStylesAuto, destStylesAuto, options, renameMap);
        }

        private string GenerateUniqueStyleName(string baseName, string family = "paragraph")
        {
            int i = 1;
            string testName;
            do
            {
                testName = $"{baseName}_s{i++}";
            } while (StyleEngine.StyleExists(testName));
            return testName;
        }

        /// <summary>
        /// 尋找或建立指定子元素。
        /// </summary>
        /// <param name="parent">父節點。</param>
        /// <param name="localName">子元素區域名稱。</param>
        /// <param name="ns">子元素命名空間 URI。</param>
        /// <param name="prefix">子元素前綴。</param>
        /// <returns>符合條件的既有或新建子元素。</returns>
        protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
            parent.AppendChild(node);
            return node;
        }

        /// <summary>
        /// 將來源文件的內容節點合併到目前文件。
        /// </summary>
        /// <param name="sourceDoc">來源文件。</param>
        /// <param name="options">合併選項。</param>
        /// <param name="renameMap">樣式重新命名對照表。</param>
        protected abstract void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap);

        /// <summary>
        /// 依樣式重新命名對照表重寫節點樹中的樣式參照。
        /// </summary>
        /// <param name="node">要處理的根節點。</param>
        /// <param name="renameMap">樣式重新命名對照表。</param>
        protected void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
        {
            var styleNameAttr = new OdfAttributeName("style-name", OdfNamespaces.Text);
            if (node.Attributes.TryGetValue(styleNameAttr, out string? currentStyleName))
            {
                if (currentStyleName != null && renameMap.TryGetValue(currentStyleName, out string? newName))
                {
                    node.Attributes[styleNameAttr] = newName;
                }
            }
            
            var drawStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Draw);
            if (node.Attributes.TryGetValue(drawStyleAttr, out string? dsName))
            {
                if (dsName != null && renameMap.TryGetValue(dsName, out string? newName))
                {
                    node.Attributes[drawStyleAttr] = newName;
                }
            }

            var tableStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Table);
            if (node.Attributes.TryGetValue(tableStyleAttr, out string? tsName))
            {
                if (tsName != null && renameMap.TryGetValue(tsName, out string? newName))
                {
                    node.Attributes[tableStyleAttr] = newName;
                }
            }

            foreach (var child in node.Children)
            {
                RemapStylesInNodes(child, renameMap);
            }
        }

        #endregion

        private void ApplySaveVersionOptions(OdfSaveOptions options)
        {
            if (options.ForceVersion is not OdfVersion forcedVersion)
            {
                return;
            }

            string version = OdfVersionInfo.ToVersionString(forcedVersion);
            Package.Version = forcedVersion;
            SetDocumentRootVersion(ContentDom, version);
            SetDocumentRootVersion(StylesDom, version);
            SetDocumentRootVersion(MetaDom, version);
            SetDocumentRootVersion(SettingsDom, version);
        }

        private static void SetDocumentRootVersion(OdfNode node, string version)
        {
            if (node.NodeType == OdfNodeType.Element)
            {
                node.SetAttribute("version", OdfNamespaces.Office, version, "office");
            }
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
        /// <param name="disposing">若為 <see langword="true"/>，則釋放受控資源。</param>
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
        /// <returns>代表非同步釋放作業的值工作。</returns>
        public async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                await Package.DisposeAsync().ConfigureAwait(false);
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 取得指定子路徑的嵌入式 ODF 文件。
        /// </summary>
        /// <typeparam name="T">嵌入式文件 wrapper 類型。</typeparam>
        /// <param name="subPath">封裝中的子路徑。</param>
        /// <returns>嵌入式文件 wrapper。</returns>
        public T GetEmbeddedDocument<T>(string subPath) where T : OdfDocument
        {
            if (string.IsNullOrEmpty(subPath)) throw new ArgumentException("Subpath cannot be null or empty.", nameof(subPath));
            if (!subPath.EndsWith("/")) subPath += "/";

            var ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage), typeof(string) });
            if (ctor != null)
            {
                return (T)ctor.Invoke(new object[] { Package, subPath });
            }
            else
            {
                ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage) });
                if (ctor != null)
                {
                    var doc = (T)ctor.Invoke(new object[] { Package });
                    doc.SubPath = subPath;
                    return doc;
                }
            }
            throw new InvalidOperationException($"Type {typeof(T).Name} does not have a compatible constructor.");
        }

        /// <summary>
        /// 建立指定子路徑的嵌入式 ODF 文件。
        /// </summary>
        /// <typeparam name="T">嵌入式文件 wrapper 類型。</typeparam>
        /// <param name="subPath">封裝中的子路徑。</param>
        /// <returns>建立完成的嵌入式文件 wrapper。</returns>
        public T CreateEmbeddedDocument<T>(string subPath) where T : OdfDocument
        {
            if (string.IsNullOrEmpty(subPath)) throw new ArgumentException("Subpath cannot be null or empty.", nameof(subPath));
            if (!subPath.EndsWith("/")) subPath += "/";

            string mimeType = typeof(T) switch
            {
                Type t when t == typeof(Presentation.PresentationDocument) => "application/vnd.oasis.opendocument.presentation",
                Type t when t == typeof(Spreadsheet.SpreadsheetDocument) => "application/vnd.oasis.opendocument.spreadsheet",
                Type t when t == typeof(OdfKit.Chart.OdfChartDocument) || t == typeof(OdfKit.Chart.ChartDocument) => "application/vnd.oasis.opendocument.chart",
                Type t when t == typeof(OdfKit.Formula.OdfFormulaDocument) || t == typeof(OdfKit.Formula.FormulaDocument) => "application/vnd.oasis.opendocument.formula",
                _ => "application/vnd.oasis.opendocument.text"
            };

            string mimePath = subPath + "mimetype";
            Package.WriteEntry(mimePath, Encoding.UTF8.GetBytes(mimeType), "");

            T doc;
            var ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage), typeof(string) });
            if (ctor != null)
            {
                doc = (T)ctor.Invoke(new object[] { Package, subPath });
            }
            else
            {
                ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage) });
                if (ctor != null)
                {
                    doc = (T)ctor.Invoke(new object[] { Package });
                    doc.SubPath = subPath;
                }
                else
                {
                    throw new InvalidOperationException($"Type {typeof(T).Name} does not have a compatible constructor.");
                }
            }

            doc.Save();
            return doc;
        }
    }
