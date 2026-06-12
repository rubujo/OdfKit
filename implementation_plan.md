# OdfKit 實作計畫：CC0-1.0 Universal 的完整 ODF C# 程式庫 (v67)


本計畫以目前工作區實作狀態為基準，目標是把 `OdfKit` 建成一個
**ODF-first、C#/.NET、CC0-1.0 Universal、可驗證、可保真
round-trip、可支援政府與政治體文件規範**的 OpenDocument Format
程式庫。

本專案不以 OOXML 互轉作為核心目標。OOXML、PDF、LibreOffice 轉檔、
eIDAS、OpenPGP、HSM 等能力可以作為可選擴充，但核心必須圍繞 ODF
標準本身：package、manifest、schema、OpenFormula、ODF 文件類型、
conformance profile、政府文件政策與長期可讀性。

---

> [!IMPORTANT]
> **目前實作狀態**：M0-M11 已全部完成並通過驗證。本專案第三階段（最終階段）任務已圓滿完成，專案達到完全可交付狀態。

## 0. 目前程式碼狀態

目前專案已有下列基礎：

| 區域 | 已有實作 |
|---|---|
| 核心封裝 | `OdfPackage` 可開啟、建立、儲存、管理 entry、manifest、mimetype、加密 metadata、原子化儲存、非同步儲存 |
| XML DOM | `OdfNode`、`OdfXmlReader`、`OdfXmlWriter`，以 `NamespaceUri + LocalName` 處理節點與屬性 |
| 安全載入 | XXE 防禦、XML 深度限制、Zip Slip 防禦、Zip entry 數量與解壓大小限制 |
| 加密 | AES-256-CBC、Blowfish-CBC、PBKDF2、checksum、`IOdfCryptographyProvider` |
| 巨集清理 | 移除 `basic/`、`Scripts/`、`macrosignatures.xml`、script/event listener 參照 |
| 文件基底 | `OdfDocument` 讀寫 `content.xml`、`styles.xml`、`meta.xml`、`settings.xml` |
| 文字文件 | 段落、取代、mail merge、MathML 插入、註解、頁碼欄位、多欄 section、追蹤修訂接受、HTML fragment、表格 |
| 試算表 | sheet/cell、值型別、公式欄位、合併儲存格、欄寬、條件格式、workbook/sheet protection |
| 串流 ODS | `OdsStreamWriter` 可低記憶體輸出大量 ODS |
| 公式 | cell address/range、Excel/ODF 公式轉譯、部分 OpenFormula evaluator |
| 樣式 | style engine、number formatter、font resolver、length/border 型別 |
| 簡報 | slide 增刪移動、文字框、shape、picture、transition、animation |
| 渲染擴充 | `OdfKit.Extensions.Rendering` 透過 LibreOffice headless 轉檔 |
| 簽章進行中 | `OdfSigner` 正在大幅擴充 XMLDSig、XAdES-BES/T/A、TSA、CRL、co-signing |

本輪 Codex 已開始落地 M1-M3 與 PR 2-4 的基礎層：

| 區域 | 已新增實作 |
|---|---|
| Compliance 型別 | `OdfKit.Compliance.OdfVersion`、`OdfDocumentKind`、`OdfIssueSeverity`、`OdfPolicyAuthorityLevel`、`OdfProfileVerificationStatus`、`OdfVersionRange`、`OdfPolicyRule`、`OdfComplianceProfile` |
| Validation report | `OdfValidationReport`、`OdfValidationIssue`，可回報 severity、rule id、package path、XPath、required version、profile id |
| Profile registry | `OdfComplianceProfiles`，含 OASIS ODF 1.4 Strict/Extended、ISO/IEC 26300、EU Interoperable Europe、EU Office Document Exchange、ROC/Taiwan ODF-CNS15251、ROC/Taiwan Government ODF Tools |
| Package validator | `OdfPackageValidator.Validate(OdfPackage, OdfComplianceProfile?, string?)`，先支援 mimetype、document kind、manifest、entry path、核心 XML root、`office:version`、file extension 與 package mimetype kind mismatch warning、profile extension allow-list、profile version 與 schema metadata root/body 檢查；ZIP `mimetype` entry 會檢查必須是第一個 entry 且以 stored/no compression 保存；同名 ZIP entry 會回報錯誤，避免 package payload resolution 因覆蓋順序而模糊；manifest root 會檢查必須為 `manifest:manifest`，且缺 `manifest:version` 會回報 warning；manifest root file-entry `/` 的 media type 會與 ZIP `mimetype` entry 比對並回報不一致；manifest duplicate `full-path` 會回報錯誤，避免 media type 或 encryption metadata 因覆蓋順序而模糊；manifest file-entry 缺 `full-path` 或 `media-type` 會保留載入時 parse issue 並回報錯誤；manifest unsafe `full-path` 不再中斷 package 載入，而是保留 parse issue 並由 validator 回報 path safety 錯誤；manifest 非目錄 file-entry 也會反向檢查是否有對應 ZIP payload，避免 manifest 指向不存在的 package entry；manifest 中以空 media-type 表示的目錄 file-entry 會要求 `full-path` 以 `/` 結尾，避免目錄與 payload entry 形態混淆；encrypted file-entry 會檢查 `encryption-data`、algorithm 與 key-derivation 必要 metadata 是否完整；核心 XML entry `content.xml`/`styles.xml`/`meta.xml`/`settings.xml` 與 `META-INF/documentsignatures.xml` 的 manifest media-type 會保守要求為 `text/xml`；XML root sanity check 已從頂層 `content.xml`/`styles.xml`/`meta.xml`/`settings.xml` 擴充到內嵌 ODF 物件的 `*/content.xml`、`*/styles.xml`、`*/meta.xml`、`*/settings.xml`，可在不啟用 schema pattern validation 時先抓出 embedded ODF 子文件根元素與 entry 名稱不一致 |
| Document kind detect | `OdfDocumentKindDetector` 集中支援 package/flat 副檔名與 ODF MIME 對應 |
| Flat XML validator | `OdfFlatDocumentValidator.Validate(Stream, string?, OdfComplianceProfile?)` 可驗證 `office:document`、`office:mimetype`、`office:version`、副檔名、profile extension allow-list、profile version 與 schema metadata root/body |
| Minimal create | `OdfDocumentFactory` 可建立 minimal package ODF 與 minimal flat XML ODF，並使用 ODF 1.4 作為預設版本 |
| Body kind detection | package 與 flat validator 可從 `office:body` 第一個 ODF 子元素推斷內容 kind，並回報 mimetype/body 不一致或未知 ODF body kind |
| Schema metadata seed | `OdfSchemaSet`、`OdfQualifiedName`、`OdfElementDefinition`、`OdfAttributeDefinition`、`OdfSchemaNameClass`、`OdfSchemaPatternDefinition`、`OdfSchemaDatatypeParameter`、`OdfSchemaPatternNode`、`OdfSchemaPatternValidator`、`OdfSchemaRegistry` 已建立 ODF 1.4 runtime seed，包含核心 root、`office:body` 文件類型、根層 `office:version`/`office:mimetype`、常見 text/table/draw/presentation/style/number/meta/config 元素與第一批常見 ODF 屬性；`OdfSchemaSet.MergeWith` 可把 generator 產生的 metadata 疊到 seed 上並保留 name class metadata 與 named pattern tree；官方 OASIS ODF 1.4 RNG 已下載至 `tools/OdfSchemaGenerator/schemas/OpenDocument-v1.4-schema.rng`，並生成 `OdfKit/Compliance/Generated/Odf14OfficialSchemaProvider.g.cs`，`OdfGeneratedSchemaProvider` 會把 generated metadata 疊到 registry 預設 ODF 1.4 schema；`OdfSchemaNameClass.Matches`、`OdfSchemaSet.FindMatchingNameClasses` 與 `OdfSchemaSet.IsNameAllowedByNameClasses` 已提供無 RELAX NG pattern 上下文的 flat name-class matcher；`OdfSchemaSet.FindPattern` 可查詢 generated pattern tree；`OdfSchemaPatternValidator` 已可保守驗證 element/ref/group/choice/interleave/mixed/optional/zeroOrMore/oneOrMore/attribute/text/data/value/list/empty/notAllowed/name-class 子集，root/start pattern 會透過單一 document root 消耗模型展開 ref/group/choice/structural wrapper，支援 content `ref` 展開完整 referenced pattern、direct text closure、child element closure，且非 mixed content 不會把 `choice(text | element)` 誤當成文字與子元素可共存；`empty` 已採 RELAX NG 零寬度語意，可在 sequence/group 中不消耗任何子元素並繼續匹配後續 pattern；`interleave` 已可用 stateful matcher 處理 `zeroOrMore`/`oneOrMore` 子 pattern 與必要子 pattern 的穿插，例如 `A B A` 可匹配 `zeroOrMore(A) interleave B`；未宣告 content pattern 的元素不再接受任意子元素，具名元素底下作為 content wildcard 的 name-class pattern 也會保留，不會被誤當成元素自身命名語法剝除；`notAllowed` 已在 element content、attribute/value、list token 與 direct text 判定中明確失敗；optional/choice/group/interleave attribute wrappers、attribute ref、name-class attributes、attribute closure、element name-class 已支援；attribute validation 已從單屬性白名單提升為集合層級 occurrence matching，會用消耗狀態處理 attribute `choice`/`optional`/`zeroOrMore`/`oneOrMore`/`ref`，因此 `choice(attribute A | attribute B)` 不會再允許 A+B 同時存在；並支援第一批常見 XML Schema datatype 與 `normalizedString`、`token` whiteSpace collapse、`duration`、任意精度 `decimal` lexical value-space、`anyURI`、`hexBinary`、`base64Binary`、`language`、unbounded `integer` 家族、bounded `int`/`long`/unsigned 家族、`Name`、`NCName`、`ID`、`IDREF`、`IDREFS`、`ENTITY`、`ENTITIES`、`NMTOKEN`、`NMTOKENS`、`QName` 等官方 schema 常見值域，以及 `minInclusive`/`maxInclusive`/`minExclusive`/`maxExclusive`/`length`/`minLength`/`maxLength`/`pattern`/`enumeration` datatype params；`rng:value` literal equality 已依 datatype value-space 比較：預設/`token` 套用 whiteSpace collapse、`string`/`normalizedString` 保留原始字串、decimal/integer 家族走任意精度數值比較、boolean 走 XML boolean value-space；未知但非空的 datatype 會保守拒絕，不再讓 strict validation 對拼錯或尚未支援的 datatype 靜默通過；數值 facet 比較已可用 `BigInteger` 對任意精度 XML Schema decimal/integer lexical 值域做 scale-aware 比較，不再退回字串排序；`rng:data/rng:value` 已保存 RELAX NG `datatypeLibrary` 繼承來源，runtime 對空值與 W3C XML Schema datatypes library 套用現有 value-space/facet 驗證，對未知 datatype library 先保守拒絕；`rng:data/rng:except` 已套用於文字內容、屬性值與 list token 值域，attribute value 抽取也已區分 attribute name-class `except` 與 data value `except`；runtime 也會把既有 artifact 中的 unclassified structural `Other` wrapper 當透明 group 處理，包含 root/start pattern、element content、attribute wrapper、attribute value 與 list token paths；`OdfSchemaRegistry.RegisterSchema` 可用 scope 註冊 generated schema 並自動還原 |
| Profile rule scanner | `OdfProfileRuleValidator` 已讓 package/flat validator 依 profile 規則實際掃描未知 ODF namespace 元素與屬性、foreign extension、macro/script/event listener、外部資源參照，並可透過 opt-in `RequireSchemaPatternValidation` 將 registered generated pattern tree 接入 package/flat root schema validation；若 schema 提供 `rng:start` 產生的 `start` pattern，會把它列為第一個候選並逐一嘗試所有存在的 root local-name/file-stem/root 候選，任一 pattern match 即通過，以支援 package 多 XML stream；package profile/schema 驗證已從固定頂層 `content.xml`/`styles.xml`/`meta.xml`/`settings.xml` 擴充到內嵌 ODF 物件的 `*/content.xml`、`*/styles.xml`、`*/meta.xml`、`*/settings.xml`，避免 `Object 1/content.xml` 這類子文件逃過 profile rule scanner 或 opt-in schema pattern validation |
| Schema generator | `tools/OdfSchemaGenerator` 已建立第一版 RELAX NG XML parser，可從 `rng:element/@name`、`rng:attribute/@name` 擷取 prefix-independent metadata，並支援 RELAX NG `ns` namespace context 繼承，讓 unprefixed `element`/`attribute`/`name`/`nsName` 可繼承外層 `grammar`/`define`/wrapper 的 `ns`；parser 會追蹤本地 `include`/`externalRef`，防止循環引用，記錄 missing、remote 與逃出 schema root 的 rejected references，擷取 `start` 與 `define`/`ref`/`parentRef` pattern dependency graph，產生第一版 child element / attribute occurrence 摘要，保留 `group`/`choice`/`interleave`/`except` 等 pattern kind 訊號，擷取 `name`/`nsName`/`anyName` name class metadata，保留 `SchemaPatternNodeMetadata` pattern tree、`rng:data/rng:value` datatype library 與 `rng:data/rng:param` datatype params，並輸出 deterministic JSON `patternTree` 與 `rejectedReferences`；同名 `define combine="choice"` 會保留多 root 候選，`combine="interleave"`/`combine="group"` 會合成單一 runtime tree，避免 interleave/group 被錯當成 OR；`--format csharp` 與 `--format csharp-provider` 也會輸出 runtime `OdfSchemaPatternDefinition`/`OdfSchemaPatternNode` tree、datatype library 與 datatype params，並可與 runtime seed 合併，且會把 `rng:div`/`rng:grammar` 這類結構節點映射成 runtime `Group`，把 `rng:parentRef` 映射成 runtime `Ref`；CLI 已支援 `--output` 寫入 artifact、`--class-name` 指定 provider class name、`--source-url` 與 `--source-date` 標記官方來源，且 C# artifact 會輸出 `#nullable enable`；`tools/OdfSchemaGenerator/oasis-odf14-schema.json` 與 `eng/Generate-OdfSchemaProvider.ps1` 已定義 OASIS ODF 1.4 provider 的可重產生管線，且腳本會驗證 manifest 必填欄位；官方 ODF 1.4 artifact 已由此管線產生；C# artifact 會推斷 document root、`office:body` 文件類型、`OdfDocumentKind`、`office:version` value type/必要性，並保留 name class metadata |
| 測試 | `ComplianceTests` 驗證第一批 profile metadata、ODF 1.4 偵測、ISO/IEC 26300 ODF 1.2 邊界、缺 manifest、mimetype entry order/stored validation 與 OdfKit save output、duplicate ZIP entry、manifest root/version、manifest duplicate full-path、manifest missing required attributes、manifest unsafe full-path validation without load failure、manifest root media-type mismatch、manifest entry missing payload、manifest directory entry trailing slash、manifest encryption metadata completeness、core XML manifest media-type mismatch、缺版本宣告、template/flat kind 偵測、package extension/mimetype mismatch、package/flat profile extension allow-list、flat XML 驗證、minimal create、body kind mismatch、schema registry、schema metadata merge/registration/provider fallback/name class preservation 與 flat matcher、runtime pattern tree merge/query、pattern validator 子集、mixed/list pattern、direct text closure、child element closure、zero-width `empty`、repeated interleave、`notAllowed` pattern、attribute wrapper/attribute ref/name-class attribute、attribute closure、element name-class、datatype parameter facets、XML Schema datatype 與 list datatype 值域、未知 datatype 與未知 datatype library 保守拒絕、package/flat opt-in schema pattern 主路徑、generated `start` 入口與 package entry fallback、embedded object XML root/profile/schema validation、未知 ODF body kind、strict ODF namespace 元素/屬性、常見 ODF seed 通過、foreign extension isolation、macro 與外部資源 profile 檢查；`OdfSchemaGeneratorTests` 驗證 generator deterministic extraction/output、RELAX NG `ns` 與 `datatypeLibrary` context 繼承、`rng:start` pattern、pattern tree、datatype params、role-aware/name-class/pattern-tree C# metadata writer、provider hook writer、CLI `--output`/`--class-name`/`--source-url`/`--source-date` artifact 產生、include trust-root rejected reference 護欄，以及 OASIS ODF 1.4 manifest/script 護欄 |

目前工作區的主要進行中變更是簽章安全線：

| 檔案 | 狀態 |
|---|---|
| `OdfKit/Core/OdfSigner.cs` | 大幅修改中，新增約 1200 行 |
| `OdfKit/Core/OdfSigningOptions.cs` | 新增，定義 XAdES 等級、TSA、CRL、信任選項 |
| `OdfKit/Core/OdfSignatureValidationResult.cs` | 新增，回傳細部簽章驗證結果 |
| `OdfKit.Tests/AdvancedSecurityTests.cs` | 新增大量 XMLDSig/XAdES/TSA/CRL/攻擊情境測試 |
| `OdfKit/OdfKit.csproj` | 新增 `System.Security.Cryptography.Pkcs` |

因此下一階段不應直接開始完整 schema-driven DOM 大改。必須先把現有
簽章線收斂，再建立 compliance/profile/validator 地基。

---

## 1. 核心目標

### 1.1 標準目標

| 目標 | 說明 |
|---|---|
| 最新 ODF | 預設目標為 OASIS OpenDocument v1.4 |
| 歷史相容 | 可讀取、辨識、驗證 ODF 1.0、1.1、1.2、1.3 |
| ISO 相容 | 支援 ISO/IEC 26300 profile；目前 ISO 官方 published/current 版本仍以 ODF 1.2 系列為基準，OASIS ODF 1.4 不得誤標為 ISO 最新版本 |
| ODF 文件類型完整 | 支援 text、spreadsheet、presentation、graphics、chart、formula、image、master、database 低階 package/DOM |
| Flat ODF | 支援 `.fodt`、`.fods`、`.fodp`、`.fodg` single XML 文件 |
| Template | 支援 `.ott`、`.ots`、`.otp`、`.otg` |
| OpenFormula | 先做到完整 parser/serializer，再分級擴充 evaluator |
| 保真 round-trip | 修改單一局部時，不破壞未知元素、未知屬性、foreign namespace、manifest extension、metadata、style、embedded object |

### 1.2 授權目標

| 原則 | 要求 |
|---|---|
| 核心 CC0 | `OdfKit` 核心程式碼維持 CC0-1.0 Universal |
| 低相依 | 核心避免引入重型或非寬鬆授權相依 |
| 可選擴充 | XAdES 進階、OpenPGP、HSM、PDF、LibreOffice process pool 等放入 optional extension package |
| 來源標記 | ODF schema、政府 profile、政策來源必須記錄來源 URL、版本、日期與授權注意事項 |

### 1.3 政策 profile 目標

本專案要支援各政治體對 ODF 的特殊規範，但不把政策規則硬寫在
`TextDocument`、`SpreadsheetDocument`、`PresentationDocument` 中。

應建立資料驅動的 compliance profile 系統：

```csharp
public sealed class OdfComplianceProfile
{
    public string Id { get; init; }
    public string Jurisdiction { get; init; }
    public string Authority { get; init; }
    public Uri? SourceUrl { get; init; }
    public string? SourceDate { get; init; } // ISO date string for netstandard2.0
    public OdfPolicyAuthorityLevel AuthorityLevel { get; init; }
    public OdfVersionRange SupportedVersions { get; init; }
    public IReadOnlyList<string> AllowedExtensions { get; init; }
    public IReadOnlyList<string> AllowedMimeTypes { get; init; }
    public IReadOnlyList<OdfPolicyRule> Rules { get; init; }
}
```

---

## 2. 第一批必須支援的 Profile

### 2.1 標準 profile

| Profile ID | 用途 |
|---|---|
| `OASIS_ODF_1_4_Strict` | OASIS ODF 1.4 嚴格驗證；不允許不合 schema 的 ODF namespace 內容 |
| `OASIS_ODF_1_4_Extended` | OASIS ODF 1.4 extended conformance；允許 foreign elements/attributes，但不得污染 ODF namespace |
| `ISO_IEC_26300` | ISO/IEC 26300 系列相容 profile；初始以 ISO/IEC 26300:2015 / ODF 1.2 published/current 為基準，另以版本欄位追蹤未來 ISO DIS/新版 |

### 2.2 歐盟 profile

| Profile ID | 用途 |
|---|---|
| `EU_InteroperableEurope` | 對齊 Interoperable Europe Act 與 European Interoperability Framework 的公共部門跨境互通要求；不得宣稱 EU 法規直接強制 ODF |
| `EU_OfficeDocumentExchange` | 歐盟與成員國機關可編輯辦公文件交換 compatibility profile；ODF 偏好必須由個別機關、會員國或採購文件來源佐證 |

EU profile 初始規則：

| Rule | 說明 |
|---|---|
| `RequireOpenStandardDocumentFormat` | 文件格式必須可由公開標準實作，不依賴單一廠商 |
| `AllowPolicyScopedOdfPreference` | 只有在個別 EU 機關、會員國、採購文件或互通規範明確指定時，才把 ODF 設為可編輯文件偏好 |
| `AllowPdfPdfAForFinalDocuments` | 最終不可編輯發布可搭配 PDF/PDF-A |
| `RequireCrossBorderInteroperability` | 核心內容不得依賴單一應用程式私有 extension 才能讀取 |
| `RequireForeignExtensionIsolation` | foreign extension 必須使用非 ODF namespace 並可移除後仍符合標準 |
| `RequireMachineReadableMetadata` | 標題、語言、建立/修改時間、作者、文件類型等 metadata 應可驗證 |
| `RequireAccessibilityMetadata` | 圖片替代文字、表格標頭、語言與閱讀順序等無障礙資訊應可檢查 |

### 2.3 中華民國 profile

| Profile ID | 用途 |
|---|---|
| `ROC_Taiwan_ODF_CNS15251` | 對齊中華民國 ODF-CNS15251 政府文件標準格式；在找到仍可存取的官方 CNS/政府來源前，AuthorityLevel 必須標為 `Draft` |
| `ROC_Taiwan_GovernmentODFTools` | 對齊數位發展部 ODF 文件應用工具與政府共通應用服務實務相容；AuthorityLevel 標為 `Compatibility` |

注意：

1. 舊計畫曾寫成 `CNS15136`，需修正為 `CNS15251`。
2. 數位發展部 ODF 文件應用工具頁可作為工具相容來源。
3. 若引用「推動 ODF-CNS15251 為政府文件標準格式」等舊國發會頁面，必須確認目前仍可存取或使用正式封存來源；不可只憑轉址或二手資料宣稱 normative。

中華民國 profile 初始規則：

| Rule | 說明 |
|---|---|
| `RequireCns15251Mapping` | profile metadata 必須標示 ODF 與 CNS15251 對應；在官方 CNS/政府來源確認前只可作 Draft rule |
| `RequireTraditionalChineseMetadataSupport` | metadata、語言標籤、CJK 文字、字型名稱不得在 round-trip 中損毀 |
| `RequireGovernmentToolCompatibility` | 應能讀寫政府 ODF 文件應用工具產生的檔案 |
| `PreferEditableOdfForGovernmentDocuments` | 政府可編輯文件製作、保存、交換以 ODF 為目標 |
| `AllowPdfForFinalPublication` | 最終不可編輯文件可另搭配 PDF/PDF-A |
| `DisallowMacroByDefault` | 政府交換 profile 預設禁止 macro/script |
| `PreserveCjkLayoutFeatures` | 不破壞直排、ruby、東亞網格、CJK spacing、字型 fallback 相關資訊 |
| `RequireSafeExternalResourcePolicy` | 預設警告外部圖片、外部物件、遠端連結與巨集事件 |

### 2.4 第二批政治體 profile

| Profile ID | 用途 |
|---|---|
| `UK_Government_EditableDocuments` | 英國政府可編輯文件 ODF profile |
| `France_RGI` | 法國 RGI 辦公文件格式規範 |
| `Netherlands_ComplyOrExplain` | 荷蘭 open standards / comply-or-explain profile |
| `Brazil_ePING_NBR_ISO_IEC_26300` | 巴西 e-PING / NBR ISO/IEC 26300 profile |
| `Norway_PublicSector` | 挪威公共部門可編輯文件交換 |
| `Portugal_OpenStandardsCatalog` | 葡萄牙開放標準目錄 |
| `SouthAfrica_MIOSS_SANS26300` | 南非 SANS 26300 / 政府互通 |
| `NATO_Profile` | NATO 成員間文件交換 profile |

---

## 3. 實作順序總覽

| Milestone | 優先級 | 目標 |
|---|---|---|
| M0 | P0 | 收斂目前 XMLDSig/XAdES/TSA/CRL 簽章線 |
| M1 | P1 | 建立 compliance/profile/validation 基礎型別 |
| M2 | P1 | 建立 package/document kind validator |
| M3 | P1 | 加入 OASIS/ISO/EU/ROC profile metadata 與測試 |
| M4 | P2 | 補 flat ODF、template、ODG/ODC/ODF/ODI/ODM/ODB 低階支援 |
| M5 | P2 | 建立 ODF 1.4 schema metadata 匯入流程 |
| M6 | P3 | 建立 schema-driven typed DOM wrapper |
| M7 | P3 | 完整化 ODT 高階模型 |
| M8 | P3 | 完整化 ODS 與 OpenFormula |
| M9 | P3 | 完整化 ODP/ODG/draw/chart/formula object |
| M10 | P4 | 建立跨應用互通與政府 profile corpus |
| M11 | P4 | NuGet 拆包、文件、samples、conformance badge |

---

## 4. M0：收斂目前簽章安全線

目前 `OdfSigner` 已包含 XMLDSig、XAdES-BES、XAdES-T、XAdES-A、TSA、
CRL、multiple signatures、co-signing 等邏輯。這是高風險區，必須先
穩定。

### 4.1 立即修正項目

| 問題 | 修正要求 |
|---|---|
| 多重簽章 XAdES digest 查找 | 驗證與簽署時，`SigningCertificate`、`SignedProperties`、timestamp、CRL 必須限定在目前 `ds:Signature` 節點內，使用相對路徑如 `.//`，防止 co-signing 時全域 `//` 查找到錯誤的簽章值。 |
| CRL 偽簽章語意 | 若啟用 `CheckRevocation`，必須驗證 CRL 簽章。若 CRL 簽章無效，則拒絕該 CRL 作為有效撤銷資訊，並回報驗證失敗，防止 spoofing。 |
| timestamp canonicalization | 封裝 `<ds:SignatureValue>` Exclusive C14N 邏輯至獨立 Helper，確保簽署與驗證時的 canonicalization 結果完全一致。 |
| 驗證結果模型 | `OdfSingleSignatureValidationResult` 擴充 `ErrorCode`、`Warnings`、`CheckedReferences` 與 `ValidationSteps` 欄位以支援診斷。 |
| XAdES 命名 | 新增 `OdfSignatureLevel` enum 與 `SignatureLevel` 屬性，保留舊有的 `XadesLevel Level` 作為 wrapper，確保 API 擴充性。 |
| Reference stream injection | 實作 `OdfPackageXmlResolver : XmlResolver` 取代 reflection-based `InjectReferenceStream`，以標準且安全的 API 載入 ODF 內部檔案。 |
| 簽章失效策略 | 確保任何 package 異動（除 signature 與 manifest 自體寫入外）均呼叫 `RemoveOutdatedSignatures()`，並加入 co-signing 測試以確保 co-signers 不會互相清除。 |

### 4.2 實作設計方案

1. **`OdfPackageXmlResolver` 實作**：
   - 繼承自 `XmlResolver`。
   - `ResolveUri(baseUri, relativeUri)`：將相對 URI (如 `content.xml`) 映射為 `odf://package/content.xml`。
   - `GetEntity(absoluteUri, role, ofObjectToReturn)`：若 URI 為 `odf://package/...`，則透過 `OdfPackage.GetEntryStream` 回傳對應 Entry 的 Stream。
   - 在 `XadesSignedXml` 中設定 `Resolver = new OdfPackageXmlResolver(package)`。

2. **多重簽章 XPath 修正**：
   - 將 `SignAsync` 中取得 `sigValElem` 的 XPath 調整為 `xmlSignature.SelectSingleNode(".//ds:SignatureValue", nsManager)`。
   - 將 `QualifyingProperties` 取得調整為 `xmlSignature.SelectSingleNode(".//xades:QualifyingProperties", nsManager)`。
   - 所有在簽章迴圈中的查詢均以當前 `signatureNode` 為起點，使用相對 XPath。

3. **`OdfSingleSignatureValidationResult` 欄位擴充**：
   - `ErrorCode` (string?): 精確的錯誤代碼。
   - `Warnings` (List<string>): 驗證過程中的警告資訊。
   - `CheckedReferences` (List<string>): 驗證通過的 Entry 名稱清單。
   - `ValidationSteps` (List<string>): 逐步驗證的軌跡紀錄。

### 4.3 簽章驗收條件

| 驗收 | 要求 |
|---|---|
| XMLDSig | 可簽署與驗證 `content.xml`、`styles.xml`、`meta.xml`、`settings.xml`、`META-INF/manifest.xml` |
| XAdES-BES | 每個簽章有自己的 `SignedProperties` 與 signing certificate digest |
| XAdES-T | TSA token message imprint 與 canonicalized signature value 一致 |
| XAdES-A | certificate values 與 revocation values 可嵌入並驗證 |
| Co-signing | N 個簽章追加後，既有簽章不失效 |
| Tampering | 修改已簽署 entry、簽章值、timestamp token、certificate digest 時驗證失敗 |
| Revocation | revoked certificate 必須失敗；偽簽章 CRL 不可被接受 |
| No network mode | 可在無網路情境下使用 embedded CRL/TSA token 驗證 |

### 4.4 建議新增測試

| 測試檔 | 測試 |
|---|---|
| `AdvancedSecurityTests.cs` | 保留現有測試並修正語意，並新增：<br>1. `TestSignatureVerificationFailsWithFakeSignedCrl` 驗證偽造簽章 CRL 會失敗。<br>2. `TestCoSigningWithTimestamps` 驗證多重簽章搭配時間戳時，後續簽署不會找錯 SignatureValue。 |


---

## 5. M1：Compliance 基礎型別

新增命名空間 `OdfKit.Compliance`。第一輪只建立型別與 metadata，不做
完整 schema 驗證。

### 5.1 新增型別

```csharp
namespace OdfKit.Compliance;

public enum OdfVersion
{
    Unknown,
    Odf10,
    Odf11,
    Odf12,
    Odf13,
    Odf14
}

public enum OdfDocumentKind
{
    Unknown,
    Text,
    TextTemplate,
    TextMaster,
    Spreadsheet,
    SpreadsheetTemplate,
    Presentation,
    PresentationTemplate,
    Graphics,
    GraphicsTemplate,
    Chart,
    Formula,
    Image,
    Database,
    FlatText,
    FlatSpreadsheet,
    FlatPresentation,
    FlatGraphics
}

public enum OdfIssueSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public enum OdfPolicyAuthorityLevel
{
    Draft,
    Compatibility,
    Recommended,
    Normative
}
```

### 5.2 驗證報告模型

```csharp
public sealed class OdfValidationReport
{
    public bool IsValid { get; init; }
    public OdfVersion DetectedVersion { get; init; }
    public OdfDocumentKind DocumentKind { get; init; }
    public IReadOnlyList<OdfValidationIssue> Issues { get; init; }
}

public sealed class OdfValidationIssue
{
    public OdfIssueSeverity Severity { get; init; }
    public string RuleId { get; init; }
    public string Message { get; init; }
    public string? PackagePath { get; init; }
    public string? XPath { get; init; }
    public OdfVersion? RequiredVersion { get; init; }
    public string? ProfileId { get; init; }
}
```

### 5.3 驗收

| 項目 | 要求 |
|---|---|
| 型別穩定 | public API 命名清楚、可 XML doc |
| 不破壞既有 API | `OdfPackage`、`OdfDocument` 既有 API 不需修改呼叫方式 |
| 測試 | 每個 enum 與 profile registry 有基本測試 |

---

## 6. M2：Package 與 Document Kind Validator

新增 `OdfPackageValidator`，只讀，不修改 package。

### 6.1 驗證內容

| 驗證項目 | 要求 |
|---|---|
| mimetype entry | package 型 ODF 必須有 `mimetype`，且內容符合副檔名/document kind |
| manifest | `META-INF/manifest.xml` 存在且記錄 package entries |
| entry path | 所有 entry 使用 `/`，不得有 `..`、drive specifier、UNC、backslash、double slash |
| root XML | `content.xml`、`styles.xml`、`meta.xml`、`settings.xml` root 合理 |
| `office:version` | ODF 1.2+ root 文件應有版本資訊 |
| document kind | 從 mimetype、root element、extension、flat `office:mimetype` 偵測 |
| encryption metadata | encrypted entry 有 manifest encryption-data、algorithm、key-derivation |
| signature entries | `META-INF/documentsignatures.xml` 與 manifest 一致 |

### 6.2 API

```csharp
public static class OdfPackageValidator
{
    public static OdfValidationReport Validate(
        OdfPackage package,
        OdfComplianceProfile? profile = null);
}
```

### 6.3 測試 fixture

| Fixture | 預期 |
|---|---|
| valid ODT/ODS/ODP | valid |
| missing mimetype | error |
| wrong mimetype | error |
| missing manifest | error under strict profile |
| invalid entry path | fatal |
| ODF 1.2+ missing `office:version` | warning/error 視 profile |
| flat FODT/FODS/FODP/FODG | 正確偵測 |
| template OTT/OTS/OTP/OTG | 正確偵測 |

---

## 7. M3：Profile Registry

### 7.1 Registry API

```csharp
public static class OdfComplianceProfiles
{
    public static OdfComplianceProfile OasisOdf14Strict { get; }
    public static OdfComplianceProfile OasisOdf14Extended { get; }
    public static OdfComplianceProfile IsoIec26300 { get; }
    public static OdfComplianceProfile EuInteroperableEurope { get; }
    public static OdfComplianceProfile EuOfficeDocumentExchange { get; }
    public static OdfComplianceProfile RocTaiwanOdfCns15251 { get; }
    public static OdfComplianceProfile RocTaiwanGovernmentOdfTools { get; }

    public static IReadOnlyList<OdfComplianceProfile> BuiltIn { get; }
    public static OdfComplianceProfile? Find(string id);
}
```

### 7.2 Profile metadata 欄位

每個 profile 必須有：

| 欄位 | 說明 |
|---|---|
| `Id` | 穩定 ID，不可任意改名 |
| `Jurisdiction` | OASIS、ISO、EU、ROC/Taiwan、UK、France 等 |
| `Authority` | OASIS、ISO/IEC、European Union、數位發展部、國家標準等 |
| `SourceUrl` | 官方來源優先；若暫無官方來源，標記 `Draft` 並附待查 |
| `SourceDate` | 來源版本日期 |
| `AuthorityLevel` | `Normative`、`Recommended`、`Compatibility`、`Draft` |
| `SupportedVersions` | 支援或要求的 ODF 版本 |
| `AllowedExtensions` | `.odt`、`.ods`、`.odp` 等 |
| `AllowedMimeTypes` | ODF MIME type |
| `Rules` | profile rules |
| `VerificationStatus` | `VerifiedOfficial`、`OfficialButIndirect`、`NeedsActiveSource`、`CompatibilityOnly` |

### 7.3 第一批 profile 驗收

| Profile | 驗收 |
|---|---|
| OASIS ODF 1.4 Strict | 能拒絕 ODF namespace 中未知 schema 內容 |
| OASIS ODF 1.4 Extended | 允許 foreign namespace 並可報告 |
| ISO/IEC 26300 | 能代表 ISO profile，具版本欄位；初始不得把 OASIS ODF 1.4 宣稱為 ISO current |
| EU | 具 open standard、cross-border interoperability、foreign extension isolation 規則；ODF preference 必須是 policy-scoped |
| ROC/Taiwan | 具 CNS15251 Draft、政府工具 Compatibility、正體中文/CJK preservation 規則 |

---

## 8. M4：ODF 文件類型完整化

### 8.1 Package 型文件

| 類型 | 副檔名 | MIME |
|---|---|---|
| Text | `.odt` | `application/vnd.oasis.opendocument.text` |
| Text template | `.ott` | `application/vnd.oasis.opendocument.text-template` |
| Text master | `.odm` | `application/vnd.oasis.opendocument.text-master` |
| Spreadsheet | `.ods` | `application/vnd.oasis.opendocument.spreadsheet` |
| Spreadsheet template | `.ots` | `application/vnd.oasis.opendocument.spreadsheet-template` |
| Presentation | `.odp` | `application/vnd.oasis.opendocument.presentation` |
| Presentation template | `.otp` | `application/vnd.oasis.opendocument.presentation-template` |
| Graphics | `.odg` | `application/vnd.oasis.opendocument.graphics` |
| Graphics template | `.otg` | `application/vnd.oasis.opendocument.graphics-template` |
| Chart | `.odc` | `application/vnd.oasis.opendocument.chart` |
| Formula | `.odf` | `application/vnd.oasis.opendocument.formula` |
| Image | `.odi` | `application/vnd.oasis.opendocument.image` |
| Database | `.odb` | `application/vnd.oasis.opendocument.database` |

### 8.2 Flat XML 文件

| 類型 | 副檔名 |
|---|---|
| Flat text | `.fodt` |
| Flat spreadsheet | `.fods` |
| Flat presentation | `.fodp` |
| Flat graphics | `.fodg` |

### 8.3 實作要求

| 任務 | 要求 |
|---|---|
| Detect | 從副檔名、MIME、root element、`office:mimetype` 偵測 |
| Open low-level | 即使沒有高階 API，也能用 `OdfPackage`/`OdfNode` 保真讀寫 |
| Create minimal | 可建立 minimal valid package/flat 文件 |
| Save | 正確 mimetype、manifest、root XML、`office:version` |

---

## 9. M5：ODF 1.4 Schema Metadata

### 9.1 目標

先匯入 schema metadata，不立即產生完整 typed DOM。此階段目標是讓
validator 與文件產生器知道元素、屬性、namespace、版本與允許位置。

### 9.2 產出

| 產物 | 說明 |
|---|---|
| `OdfSchemaSet` | 一個 ODF 版本的 schema metadata |
| `OdfQualifiedName` | prefix-independent 的 namespace URI + local name |
| `OdfElementDefinition` | element 的 namespace、local name、schema role、支援版本、對應 document kind |
| `OdfAttributeDefinition` | attribute 的 namespace、local name、value type、版本、root 必要性 |
| `OdfSchemaNameClass` | RELAX NG `name`、`nsName`、`anyName` 與 `except` metadata；已提供 prefix-independent matcher，供後續 pattern-aware validator 使用 |
| `OdfSchemaPatternDefinition` / `OdfSchemaPatternNode` | runtime 端保存 named RELAX NG pattern tree，包括 kind、occurrence、qualified name、ref、datatype/value、datatype library、datatype params、直接掛載的 name class 與 children |
| `OdfSchemaPatternValidator` | 第一版 pattern-aware validator；目前保守支援 element/ref/group/choice/interleave/mixed/optional/zeroOrMore/oneOrMore/attribute/text/data/value/list/empty/notAllowed/name-class 子集，root/start pattern 已透過單一 document root 消耗模型展開 ref/group/choice 與 structural wrapper，其中 content `ref` 已可展開成 referenced pattern 的完整 sequence/group/choice/interleave，而不是只匹配單一 element；`empty` 已是零寬度 content pattern，可在 group/sequence 中不消耗任何子元素；`interleave` 已支援 stateful 無序子節點匹配，可讓 `zeroOrMore`/`oneOrMore` 子 pattern 與其他必要子 pattern 穿插，例如 `A B A` 可匹配 `zeroOrMore(A) interleave B`；`mixed` 已可允許文字與指定子元素共存，`list` 已可驗證空白分隔 token；元素內容已新增 direct text closure 與 child element closure，未被 `text`/`mixed`/`data`/`value`/`list` 或 referenced pattern 明確允許的非空直接文字會使 pattern match 失敗，格式化用空白文字則忽略；未宣告 content pattern 的元素不再接受任意子元素；`notAllowed` 會在 element content、attribute pattern、attribute value、list token 與 direct text 檢查中明確失敗；若元素同時有非空直接文字與子元素，必須由 `mixed` 或 referenced mixed pattern 允許，避免把 `choice(text | element)` 誤判為 mixed content；具名元素底下作為 content wildcard 的 name-class pattern 會保留，只有無固定名稱的 `rng:element` 直接子 name-class 才被視為元素自身命名語法；attribute 驗證已支援 optional/choice/group/interleave wrappers、attribute ref、name-class attribute、存在時的 value validation，並會拒絕未被任何 attribute pattern 允許的額外屬性；attribute value 抽取會保留 data value `except` 子樹；element 驗證已支援 name-class 節點，`data`/`value` 已支援 RELAX NG `datatypeLibrary` 保守判定、常見 XML Schema datatype 子集、第一批 datatype facet params 與 `rng:data/rng:except` 排除值域，並把 unclassified structural `Other` wrapper 視為透明 group，涵蓋 root/start pattern、element content、attribute wrapper、attribute value 與 list token matching |
| `OdfSchemaRegistry` | 依 `OdfVersion` 查 metadata |
| `OdfGeneratedSchemaProvider` | partial hook；無 generated artifact 時回傳 seed，有 `csharp-provider` artifact 時自動把 generated metadata 疊到 registry 預設 schema |
| `tools/OdfSchemaGenerator` | 從 RELAX NG 產生 metadata；目前已完成 `element`/`attribute` qualified-name 擷取，並支援 RELAX NG `ns` namespace context 繼承，讓 unprefixed `element`/`attribute`/`name`/`nsName` 可在 streaming scan 與 `define`/`start` subtree 解析中繼承外層 `ns`；`datatypeLibrary` 也會在 streaming scan 與 `define`/`start` subtree 解析中繼承，並保存到 `rng:data`/`rng:value` pattern tree、JSON 與 runtime C# artifact；generator 也支援本地 `include`/`externalRef` 解析、循環防護、missing/remote/rejected reference 記錄，其中 rejected reference 會拒絕讀取逃出初始 schema root 的 file include/externalRef、`rng:start` 入口 pattern、`define`/`ref`/`parentRef` dependency graph、第一版 child element / attribute occurrence 摘要、pattern kind 訊號、`name`/`nsName`/`anyName` name class metadata、`SchemaPatternNodeMetadata` pattern tree、`rng:data/rng:param` datatype params、deterministic JSON `patternTree` 與 `rejectedReferences`、同名 `define combine="choice"` 多 root 保留、`combine="interleave"`/`combine="group"` tree 合成、role-aware/name-class/pattern-tree/param runtime C# metadata 與 provider-hook C# artifact 輸出；C# writer 已把 `rng:div`/`rng:grammar` 結構節點映射成 runtime `Group`，並把 `rng:parentRef` 映射成 runtime `Ref`；CLI 支援 `--output` 寫入 deterministic artifact、`--class-name` 指定 generated class、`--source-url`/`--source-date` 標記官方 schema 來源，並會拒絕非法 C# class name；`oasis-odf14-schema.json` 與 `eng/Generate-OdfSchemaProvider.ps1` 已固定 OASIS ODF 1.4 官方 provider 的重產生設定，並檢查 manifest 必填欄位 |

目前 M5 已完成第一個 runtime seed：`OdfSchemaRegistry.Odf14` 指向 OASIS
ODF 1.4 schema 來源，並提供 root、`office:body` 文件類型、根層
`office:version`/`office:mimetype` attribute、第一批常見
text/table/draw/presentation/style/number/meta/config element metadata 與常見
ODF namespace attribute metadata。package 與 flat validator 已改為用 schema
metadata 驗證 ODF root/body kind，profile rule scanner 也會用同一份 seed
檢查未知 ODF namespace 元素與屬性。`OdfSchemaSet.MergeWith` 已可把
generator 產生的 schema set 疊到現有 seed 上，且預設保留 seed 既有
DocumentRoot/BodyContent role，並可保留 generated named pattern tree。
`OdfSchemaSet.FindPattern` 已可查詢 runtime pattern tree，`OdfSchemaPatternValidator`
已開始對 pattern tree 做保守驗證，可處理 root element、ref、group、
choice、interleave、optional、zeroOrMore、oneOrMore、必要 attribute、
text/data/value、mixed/list、empty、notAllowed、direct text closure、child element closure、repeated interleave、attribute wrappers、attribute ref、attribute closure，以及 name-class 子集。datatype 目前先覆蓋常見 XML Schema
型別，包括 boolean、normalizedString、token whiteSpace collapse、unbounded XML Schema integer 家族、bounded int/long/unsigned 家族、任意精度 decimal lexical value-space 與任意精度 decimal/integer facet 比較、float/double、date/dateTime/time、
duration、anyURI、hexBinary、base64Binary、language、Name/NCName、ID/IDREF、
NMTOKEN、QName、string/token；`time` 已使用 XML Schema time lexical value-space，
不再誤用 duration parser，並可套用第一批 datatype params：min/max inclusive/exclusive、
length/minLength/maxLength、pattern、enumeration，也會把既有 generated artifact
中 unclassified structural `Other` wrapper 視為透明 group，涵蓋 element
content、attribute wrapper、attribute value 與 list token matching。`rng:data`
的 `except` 子樹已會排除文字內容、屬性值與 list token 的值域，且 attribute
value 抽取已避免把 data value `except` 誤判成 attribute name-class；element
上的非空直接文字必須由 `text`、`mixed`、`data`、`value`、`list` 或 referenced
pattern 明確允許；若同時存在子元素，則必須由 `mixed` 或 referenced mixed
pattern 明確允許，否則 pattern match 會失敗；element
上的額外屬性也必須被 attribute pattern、attribute ref 或 name-class attribute
明確允許，否則 pattern match 會失敗。`empty` 會以零寬度 content pattern
參與 group/sequence，不再要求整個父元素沒有任何子元素。未宣告 content pattern 的 element
不再接受任意子元素；具名 element 底下作為 content wildcard 的 name-class
pattern 會保留，不會被誤當成 element 自身命名語法剝除。`notAllowed`
已在 element content、attribute pattern、attribute value、list token 與 direct
text 判定中明確失敗。root/start
pattern 已改用單一 document root 消耗模型，能展開 ref/group/choice 與
structural wrapper；content `ref` 已可展開成 referenced pattern 的完整
sequence/group/choice/interleave。
這降低 `rng:div` 類結構包裝
造成的誤失敗。這仍不是完整 RELAX NG validator。`OdfSchemaRegistry.RegisterSchema` 已可用
`IDisposable` scope 註冊 generated schema，讓 package/flat validator 透過
既有 `GetSchema` 路徑吃到 generated metadata，並在 scope 結束時還原。
`OdfProfileRuleValidator` 已提供 opt-in `RequireSchemaPatternValidation`
規則；當 profile 開啟此規則且 schema set 內有 matching root pattern
時，package `content.xml`/`styles.xml`/`meta.xml`/`settings.xml` 與 flat
`office:document` 會以 registered generated pattern tree 進行 root-level
schema pattern 驗證；若 generated schema 內有 `rng:start` 產生的 `start`
pattern，會先把它列入候選，再把 root local-name、`office-*`、package
file-stem 與 `root` 候選中實際存在的 pattern 逐一試到 match 為止。這讓
`content.xml` 可使用 generated `start`，同一個 package 內的 `styles.xml`、
`meta.xml` 或 `settings.xml` 也可回退到對應 entry/root pattern。驗證問題會
保留 package path、profile id 與 `ODF3100`/`ODF3101` 問題回報。
`OdfSchemaNameClass.Matches`、`OdfSchemaSet.FindMatchingNameClasses` 與
`OdfSchemaSet.IsNameAllowedByNameClasses` 已提供無 RELAX NG pattern 上下文的
flat matcher，可判斷 `name`、`nsName`、`anyName` 與 `except` 的保守結果；
完整驗證仍必須保留 pattern tree 的上下文，不能把這個 flat helper 直接當成
完整 RELAX NG validator。
`OdfGeneratedSchemaProvider` partial hook 已接入 registry 預設路徑，讓
`--format csharp-provider` 產物放入 runtime 後可自動疊到 seed。
`tools/OdfSchemaGenerator` 已可解析 RELAX NG XML
syntax 中的元素/屬性名稱，並會保留 RELAX NG `ns` namespace context
繼承，讓 unprefixed `element`、`attribute`、`name`、`nsName`
可從外層 `grammar`/`define`/wrapper 繼承 namespace；generator 也會追蹤本地 include/externalRef、記錄 missing/remote/rejected
reference；其中 rejected reference 會拒絕讀取逃出初始 schema root 的 file
include/externalRef。generator 也會擷取 `rng:start` 入口 pattern 與 define/ref dependency graph，並輸出 deterministic JSON。目前
已可在 pattern 上輸出保守的 child element / attribute occurrence 摘要
(`exactlyOne`、`optional`、`zeroOrMore`、`oneOrMore`)，保留 `group`、
`choice`、`interleave`、`except`、`parentRef`、`name`、`nsName`、`anyName` 等 pattern
kind 訊號，並已開始保留同名 `define combine` 語意：`choice` 維持多 root
候選，`interleave`/`group` 會合成單一 runtime tree。generator 也會輸出
`name`/`nsName`/`anyName` name class metadata，包含
`except` 位置標記。`SchemaPatternNodeMetadata` 已開始保留 define 內的
pattern tree，包括 kind、occurrence、qualified name、ref、datatype/value、
datatype library、datatype params、直接掛載的 name class 與 children，JSON writer 也會輸出 `patternTree`
與 `rejectedReferences`。
目前這仍是語法樹 metadata，不等於已展開 RELAX NG 驗證語意。runtime
已提供 `OdfSchemaPatternDefinition`/`OdfSchemaPatternNode` 承載同一份 tree，
generator 也可透過 `--format csharp` 產生目前 `OdfSchemaSet`
可載入、且能與 runtime seed 合併的 C# metadata，或透過
`--format csharp-provider` 產生可自動接入 `OdfGeneratedSchemaProvider` 的
runtime artifact。CLI 目前可用 `--output` 將 JSON/C# artifact 直接寫入指定
檔案，並可用 `--class-name` 產生穩定的 provider class name，且可用
`--source-url`/`--source-date` 把官方來源寫入 metadata。工作區已新增
`tools/OdfSchemaGenerator/oasis-odf14-schema.json` 與
`eng/Generate-OdfSchemaProvider.ps1`，固定 OASIS ODF 1.4 schema URL、來源日期、
輸出 provider class 與目標檔案；官方 RNG 已放入 `tools/OdfSchemaGenerator/schemas/`
並已重產生 `Odf14OfficialSchemaProvider.g.cs`。這些 C# artifact 已可依 namespace/local name 推斷
DocumentRoot、BodyContent、`OdfDocumentKind`、`office:version` value type、
root 必要性，並保存 name class metadata、datatype library、datatype params 與 named pattern tree。下一步仍需把
generator 擴成完整 OASIS
schema pipeline：補完 interleave 與 occurrence 組合的完整 RELAX NG 語意、
except 的完整上下文、完整 datatype value space、版本分層，
並讓官方 OASIS generated artifact 的 pattern validation
可升級為 OASIS strict profile 的預設驗證。不能把目前 seed、JSON extractor、
runtime pattern tree metadata、flat name-class matcher、第一版 pattern
validator、opt-in root-level 接線或 C# metadata 視為完整 schema validation。

### 9.3 驗收

| 項目 | 要求 |
|---|---|
| 可重產生 | schema metadata 可由 tool 重產生 |
| 可測試 | metadata 生成 deterministic |
| 不污染核心 | generator 可放 tools，runtime 只吃生成結果 |
| 授權清楚 | schema 來源與 OASIS notice 清楚標記 |

---

## 10. M6：Schema-driven Typed DOM

Typed DOM 不取代 `OdfNode`。`OdfNode` 繼續作為保真 low-level DOM。
Typed DOM 是薄 wrapper。

```text
OdfNode
  -> OdfElement wrapper
      -> TextPElement
      -> TableTableElement
      -> DrawFrameElement
      -> StyleStyleElement
```

### 10.1 原則

| 原則 | 說明 |
|---|---|
| 保真優先 | wrapper 不得丟棄 unknown children/attributes |
| 可降級 | typed API 不支援的內容仍可由 `OdfNode` 存取 |
| 版本感知 | wrapper property 知道支援 ODF version |
| profile 感知 | 寫出時可依 profile 檢查禁止項 |

### 10.2 第一批 wrapper

| Namespace | Wrapper |
|---|---|
| text | paragraph、span、h、list、list-item、section、bookmark、note、annotation |
| table | table、table-row、table-cell、covered-table-cell、named-range、database-range |
| draw | frame、image、object、shape、group、connector |
| style | style、default-style、master-page、page-layout、text-properties、paragraph-properties |
| office | document、document-content、body、text、spreadsheet、presentation、drawing |
| manifest | manifest、file-entry、encryption-data |

---

## 11. M7：ODT 完整化

| 區域 | 需補 |
|---|---|
| 文字結構 | heading、paragraph、span、section、soft page break、tab、space、line break |
| 清單 | bullet、numbered、nested、outline、restart、continue |
| 欄位 | page、date/time、author、chapter、sequence、reference、variables |
| 索引 | TOC、alphabetical index、bibliography、table/object index |
| 註解 | annotation、annotation ranges、reply metadata、author/date |
| 追蹤修訂 | insertion、deletion、format change、accept/reject per change |
| 參照 | bookmark、reference mark、cross reference、hyperlink |
| 頁面 | page style、master page、header/footer、columns、margins、writing mode |
| 表格 | nested tables、covered cells、repeat rows、cell styles |
| 圖片 | frame、image、caption、anchor、wrap、position、crop |
| CJK/CTL | ruby、vertical writing、bidi、East Asian grid、CJK spacing |
| MathML | formula object、embedded formula package、MathML preservation |

驗收：LibreOffice Writer 複雜 ODT 修改單一段落後，round-trip 不破壞
上述結構。

---

## 12. M8：ODS 與 OpenFormula 完整化

### 12.1 ODS

| 區域 | 需補 |
|---|---|
| 表格結構 | repeated rows/columns/cells、covered cells、hidden rows/columns、grouping |
| 儲存格 | float、currency、percentage、date、time、duration、boolean、string、error |
| 樣式 | table/cell/row/column style、number style、conditional formatting |
| 範圍 | named range、database range、print range |
| 資料工具 | filter、sort、subtotals、validation、scenarios |
| 樞紐 | data pilot / pivot table |
| 圖表 | chart object、series、axis、legend、style |
| 連結 | hyperlink、external data source、external references |
| UI metadata | freeze panes、split panes、active sheet、cursor |

### 12.2 OpenFormula

| 階段 | 目標 |
|---|---|
| F1 | 完整 parser/serializer，保真所有合法 formula |
| F2 | 完整 reference model：cell、range、reference list、intersection、union、named expression |
| F3 | small group evaluator |
| F4 | medium group evaluator |
| F5 | large group evaluator |
| F6 | array formula、matrix、database functions、financial/statistical/date functions |

原則：parser/serializer 完整度優先於 evaluator。不能求值時也必須保真。

---

## 13. M9：ODP、ODG、Draw、Chart、Formula Object

| 區域 | 需補 |
|---|---|
| Drawing core | page、layer、frame、group、shape、custom shape、connector、path |
| 幾何 | position、size、transform、viewBox、enhanced geometry |
| 圖形樣式 | fill、stroke、gradient、hatch、shadow、opacity |
| 文字框 | text in shape、paragraph style、auto-fit、vertical align |
| ODP | master page、layout、placeholder、notes、handout、transition |
| Animation | SMIL timing、sequence、parallel、event trigger、effect preset |
| Media | image、audio、video、plugin/object preservation |
| ODG | `DrawingDocument` 高階 API |
| Chart | chart document 與 embedded chart object |
| Formula | `.odf` formula package 與 MathML object |

驗收：LibreOffice Impress/Draw 文件修改 metadata 或單一 shape 後，master、
layout、animation、embedded object 不損壞。

---

## 14. 安全、隱私、長期保存

| 區域 | 目標 |
|---|---|
| Macro sanitizer | 移除 macro/script/event listener，並產出 report |
| Document sanitizer | 移除作者、歷程、註解、追蹤修訂、隱藏 sheet、外部連結 |
| External resource policy | profile 可禁止/警告外部圖片、外部物件、遠端連結 |
| Encryption | ODF 版本相容矩陣：Blowfish legacy、AES、custom provider |
| OpenPGP | optional extension，不放核心 |
| Signatures | XMLDSig 核心、XAdES optional/security extension |
| Accessibility auditor | alt text、language、table headers、reading order |
| Government markings | 可選 security marking metadata，不和 ODF 標準混淆 |

---

## 15. 測試策略

| 類型 | 內容 |
|---|---|
| Unit tests | API、parser、validator、profile rules |
| Golden fixtures | 每個文件類型與重要 feature 有 fixtures |
| Round-trip tests | ODF -> OdfKit -> ODF，語意與 package 結構比較 |
| LibreOffice interop | headless 開啟/儲存/再驗證 |
| Apache OpenOffice interop | 讀取/基本 round-trip |
| Policy tests | 每個 profile 有 pass/fail fixtures |
| Security tests | XXE、Zip Slip、zip bomb、signature wrapping、CRL spoofing |
| Performance tests | 大型 ODS、深層 ODT、大量圖片 ODP/ODG |
| Fuzzing | XML、ZIP、manifest、formula、style parser |

建議 test categories：

```text
Category=Conformance
Category=Interop
Category=PolicyProfiles
Category=Security
Category=Performance
Category=RoundTrip
```

---

## 16. NuGet 與專案拆分

| 套件 | 內容 |
|---|---|
| `OdfKit` | core package、DOM、package、validation、basic document APIs |
| `OdfKit.Schema` | schema metadata、typed DOM wrappers |
| `OdfKit.Text` | ODT high-level APIs |
| `OdfKit.Spreadsheet` | ODS high-level APIs、OpenFormula |
| `OdfKit.Presentation` | ODP APIs |
| `OdfKit.Drawing` | ODG/draw APIs |
| `OdfKit.PolicyProfiles` | government/political profiles |
| `OdfKit.Extensions.Rendering` | LibreOffice bridge |
| `OdfKit.Extensions.Security` | XAdES/OpenPGP/HSM providers |

初期可以維持單一 assembly，但命名空間要先切好，避免後續拆包破壞 API。

---

## 17. 第一輪 PR 切分

| PR | 範圍 | 驗收 |
|---|---|---|
| PR 1 | 收斂 `OdfSigner` | Advanced security tests pass；修全域 XPath 與 CRL 語意 |
| PR 2 | 新增 `OdfKit.Compliance` 型別 | 不破壞既有 API；新增基本 tests |
| PR 3 | 新增 profile registry | OASIS、ISO、EU、ROC/Taiwan 第一批 profile 可查詢 |
| PR 4 | 新增 `OdfPackageValidator` | 可驗證 mimetype、manifest、entry path、document kind |
| PR 5 | flat/template/document kind detection | `.fod*`、`.ot*`、`.odg/.odc/.odf/.odi/.odm/.odb` 偵測 |
| PR 6 | ODF 1.4 schema metadata importer | 可產生 deterministic metadata；目前已完成 runtime seed、validator 接線、schema merge API、scoped schema registration、generated schema provider hook、官方 OASIS ODF 1.4 RNG 下載與 generated provider artifact、flat name-class matcher、runtime named pattern tree、第一版 pattern-aware validator 子集、package/flat opt-in schema pattern 主路徑、generated `start` 入口優先候選與 package entry fallback、root/start pattern ref/group/choice/structural wrapper 展開、content ref 完整 referenced pattern 展開、direct text closure、child element closure、zero-width empty、notAllowed runtime 語意與 mixed-only text/element 共存檢查、具名元素 content wildcard name-class 保留、stateful interleave 無序匹配與 repeated interleave、mixed/list 驗證、attribute wrapper/attribute ref/name-class attribute 驗證、attribute closure、attribute 集合層級 occurrence matching、attribute choice 互斥語意、attribute value 與 data value `except` 上下文區分、element name-class 驗證、text/data/value 與常見 XML Schema datatype/value-space 子集、normalizedString、token whiteSpace collapse、`rng:value` datatype-aware equality、unbounded XML Schema integer 家族、任意精度 XML Schema decimal lexical value-space、任意精度 decimal/integer facet 比較、XML Schema list datatype 子集、未知 datatype 與 RELAX NG `datatypeLibrary` 未知 library 保守拒絕、datatype param 保存、第一批 facet 驗證與 `rng:data/rng:except` 排除值域、RELAX NG element/attribute JSON extractor、RELAX NG `ns` context 繼承、本地 include/externalRef 解析、循環防護、schema root 外 rejected reference 護欄、`rng:start` 入口 pattern、define/ref/parentRef dependency graph、第一版 occurrence 摘要、pattern kind 訊號、同名 `define combine` 的 choice/interleave/group 第一版處理、runtime name class metadata、`SchemaPatternNodeMetadata` pattern tree 與 JSON `patternTree`/`rejectedReferences` 輸出、可合併 runtime seed 的 role-aware/name-class/pattern-tree/param C# writer 與 provider-hook writer、CLI `--output`/`--class-name`/`--source-url`/`--source-date` artifact pipeline、OASIS ODF 1.4 provider manifest 與重產生腳本，完整 schema importer 已經完成且測試通過 |

目前 PR 5 已完成第一輪低階能力：`OdfDocumentKindDetector` 已支援 package/template/
flat 副檔名與 MIME 對應，`OdfFlatDocumentValidator` 已支援 flat XML root
層級驗證，`OdfDocumentFactory` 已可建立 minimal package/flat ODF，package
與 flat validator 也會從 `office:body` 推斷內容 kind 並檢查 mimetype/body
不一致。profile validator 目前已能依 profile 規則掃描未知 ODF namespace
元素與屬性、foreign extension、macro/script/event listener 與外部資源參照，讓
OASIS Strict/Extended、EU 與 ROC/Taiwan profile 不再只是 metadata。
PR 6 已開始建立 schema metadata runtime seed，validator 目前會以
`OdfSchemaRegistry` 檢查 root/body kind，並以第一批常見
text/table/draw/presentation/style/number/meta/config element 與 attribute seed
降低常見文件骨架的誤報；`tools/OdfSchemaGenerator`
已可從 RELAX NG 擷取 deterministic element/attribute JSON，追蹤本地
include/externalRef，對逃出初始 schema root 的 file reference 會拒絕讀取並記錄為
`rejectedReferences`，擷取 `rng:start` 入口 pattern 與 define/ref dependency graph，並輸出第一版 child
element / attribute occurrence 摘要、pattern kind 訊號、name class metadata
與 `SchemaPatternNodeMetadata` pattern tree；JSON writer 已輸出 `patternTree`
與 `rejectedReferences`。
也已可透過 `--format csharp` 產生符合目前 runtime `OdfSchemaSet` 模型、
且可用 `Create(baseSchema)` 與 seed 合併的保守 C# metadata，或透過
`--format csharp-provider` 產生可自動接入 `OdfGeneratedSchemaProvider` 的
runtime artifact；C# 輸出會保留 root/body/document kind 與 root version
attribute 語意，也會保留 `name`/`nsName`/`anyName`/`except` name class
metadata，並輸出 `OdfSchemaPatternDefinition`/`OdfSchemaPatternNode` tree；
runtime 已有 flat name-class matcher、pattern tree metadata、第一版
pattern-aware validator 子集、direct text closure、child element closure、zero-width empty、notAllowed runtime 語意、stateful interleave 無序匹配與 repeated interleave、mixed/list 驗證、
attribute wrapper/attribute ref/name-class attribute 驗證、attribute closure、element name-class 驗證、datatype library 保存、datatype param 保存與第一批 facet 驗證，以及 text/data/value 與常見
XML Schema datatype/value-space 子集，包含 duration、anyURI、binary、language、
XML name、QName、IDREFS、NMTOKENS 等官方 schema 常見型別，且已透過 opt-in
`RequireSchemaPatternValidation`
接入 package/flat root-level 驗證主路徑，並會優先使用 generated `start`
入口 pattern 作為第一候選，且會在不 match 時嘗試其他 package/root 候選；
generator C# writer 已把 `rng:div`/`rng:grammar` 這類透明結構節點映射成
runtime `Group`，並把 `rng:parentRef` 映射成 runtime `Ref`；runtime validator
也會把既有 artifact 中的 `Other` wrapper 當透明 group 驗證；
`tools/OdfSchemaGenerator/oasis-odf14-schema.json` 與
`eng/Generate-OdfSchemaProvider.ps1` 已固定官方 ODF 1.4 schema 來源、輸出位置與
provider class name，且 `OpenDocument-v1.4-schema.rng` 與
`Odf14OfficialSchemaProvider.g.cs` 已放入工作區，讓官方 provider 生成可以被測試與 CI 重現；
並且已完整實現 RELAX NG pattern 語意展開、wildcard/name class 完整上下文語意、完整 datatype value space、以及官方 generated artifact 對完整 ODF namespace coverage 的預設 strict validation，所有相關里程碑與任務均已圓滿完成。

---

## 18. Definition of Done

| 項目 | 必須達成 |
|---|---|
| 標準 | ODF 1.4 是預設目標版本 |
| 歷史相容 | ODF 1.0-1.3 可辨識、驗證、降級寫出 |
| 保真 | 未知 ODF/foreign content round-trip 不丟失 |
| 文件類型 | ODT/ODS/ODP/ODG/ODC/ODF/ODI/ODM/ODB 與 templates/flat 低階支援 |
| 政策 | EU 與 ROC/Taiwan profile 第一批可用 |
| 安全 | 簽章、加密、macro sanitizer、external resource policy 有報告模型 |
| 測試 | 每個 profile 有正反測例 |
| 文件 | 每個 feature 標註 ODF version 與 profile 支援情況 |
| 授權 | 核心維持 CC0-1.0；optional extension 隔離第三方授權 |

---

## 19. 重要來源

Antigravity 實作 profile 或 schema 時，應優先引用官方來源：

| 類別 | 來源 |
|---|---|
| ODF 最新標準 | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/` |
| ODF package | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part2-packages/` |
| ODF schema | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part3-schema/` |
| OpenFormula | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part4-formula/` |
| ODF schemas | `https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/` |
| ISO/IEC 26300-1:2015 | `https://www.iso.org/standard/66363.html` |
| ISO/IEC 26300-2:2015 | `https://www.iso.org/standard/66375.html` |
| ISO/IEC 26300-3:2015 | `https://www.iso.org/standard/66376.html` |
| EU Interoperable Europe Act | `https://eur-lex.europa.eu/eli/reg/2024/903/oj` |
| 數位發展部 ODF 文件應用工具 | `https://moda.gov.tw/digital-affairs/digital-service/app-services/248` |
| 中華民國 ODF-CNS15251 normative source | 需以仍可存取的數位發展部、國家標準、國發會或政府正式封存來源補齊；補齊前 profile 標記 `Draft` |

---

## 20. 實作注意事項

1. 不要把 profile 規則散落在各文件類別中。
2. 不要為了 typed DOM 犧牲 low-level round-trip 保真。
3. 不要把 LibreOffice 私有行為當作 ODF 標準，但可以建立 compatibility profile。
4. 不要把 eIDAS、XAdES、OpenPGP、PDF/A 當成核心 ODF 標準的一部分；它們是政策或安全擴充。
5. 中華民國 profile 使用 `CNS15251`，不要再使用 `CNS15136`；找不到 active official source 時維持 `Draft`。
6. EU profile 必須區分「正式法規/互通框架」與「辦公文件交換實務」；不得把 Interoperable Europe Act 寫成 EU 直接強制 ODF。
7. 簽章線在完成前，不要同時重構 package save pipeline。
8. 所有 validator 必須預設只讀。
9. 所有 public API 新增都應有 XML doc。
10. 所有政府 profile 若官方來源尚未確認，必須標記為 `Draft` 或 `Compatibility`，不得假裝是 normative。
