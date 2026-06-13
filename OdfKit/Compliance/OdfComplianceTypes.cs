#pragma warning restore CS1591

using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 識別 OdfKit 支援的開放文件格式 (OpenDocument Format, ODF) 版本。
/// </summary>
public enum OdfVersion
{
    /// <summary>
    /// 未知或無法偵測的版本。
    /// </summary>
    Unknown,

    /// <summary>
    /// OpenDocument 格式 1.0。
    /// </summary>
    Odf10,

    /// <summary>
    /// OpenDocument 格式 1.1。
    /// </summary>
    Odf11,

    /// <summary>
    /// OpenDocument 格式 1.2 或 ISO/IEC 26300:2015。
    /// </summary>
    Odf12,

    /// <summary>
    /// OpenDocument 格式 1.3。
    /// </summary>
    Odf13,

    /// <summary>
    /// OpenDocument 格式 1.4。
    /// </summary>
    Odf14
}

/// <summary>
/// 識別封裝容器或單一 XML 文件所代表的 ODF 文件種類。
/// </summary>
public enum OdfDocumentKind
{
    /// <summary>
    /// 未知或無法偵測的文件種類。
    /// </summary>
    Unknown,

    /// <summary>
    /// ODF 文字文件 (.odt)。
    /// </summary>
    Text,

    /// <summary>
    /// ODF 文字範本 (.ott)。
    /// </summary>
    TextTemplate,

    /// <summary>
    /// ODF 主控文字文件 (.odm)。
    /// </summary>
    TextMaster,

    /// <summary>
    /// ODF 試算表文件 (.ods)。
    /// </summary>
    Spreadsheet,

    /// <summary>
    /// ODF 試算表範本 (.ots)。
    /// </summary>
    SpreadsheetTemplate,

    /// <summary>
    /// ODF 簡報文件 (.odp)。
    /// </summary>
    Presentation,

    /// <summary>
    /// ODF 簡報範本 (.otp)。
    /// </summary>
    PresentationTemplate,

    /// <summary>
    /// ODF 繪圖文件 (.odg)。
    /// </summary>
    Graphics,

    /// <summary>
    /// ODF 繪圖範本 (.otg)。
    /// </summary>
    GraphicsTemplate,

    /// <summary>
    /// ODF 圖表文件 (.odc)。
    /// </summary>
    Chart,

    /// <summary>
    /// ODF 公式文件 (.odf)。
    /// </summary>
    Formula,

    /// <summary>
    /// ODF 影像文件 (.odi)。
    /// </summary>
    Image,

    /// <summary>
    /// ODF 資料庫文件 (.odb)。
    /// </summary>
    Database,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 文字文件 (.fodt)。
    /// </summary>
    FlatText,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 試算表文件 (.fods)。
    /// </summary>
    FlatSpreadsheet,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 簡報文件 (.fodp)。
    /// </summary>
    FlatPresentation,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 繪圖文件 (.fodg)。
    /// </summary>
    FlatGraphics,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 圖表文件 (.fodc)。
    /// </summary>
    FlatChart,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 公式文件 (.fdf)。
    /// </summary>
    FlatFormula,

    /// <summary>
    /// 單一 Flat XML 格式的 ODF 影像文件 (.fodi)。
    /// </summary>
    FlatImage
}

/// <summary>
/// 指示合規性驗證問題的嚴重性等級。
/// </summary>
public enum OdfIssueSeverity
{
    /// <summary>
    /// 資訊性探索結果，僅供參考。
    /// </summary>
    Info,

    /// <summary>
    /// 非致命的警告，不影響核心讀寫。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤，表示文件不符合選定的合規規範。
    /// </summary>
    Error,

    /// <summary>
    /// 致命錯誤，將阻止可靠的驗證程序。
    /// </summary>
    Fatal
}

/// <summary>
/// 描述合規性規範的權威等級。
/// </summary>
public enum OdfPolicyAuthorityLevel
{
    /// <summary>
    /// 草案規範，其規範性來源尚待確認。
    /// </summary>
    Draft,

    /// <summary>
    /// 相容性規範，源自特定工具或部署的行為。
    /// </summary>
    Compatibility,

    /// <summary>
    /// 推薦規範，由相關政策指導方針所支持。
    /// </summary>
    Recommended,

    /// <summary>
    /// 標準規範，由正式標準或具約束力的法律要求支持。
    /// </summary>
    Normative
}

/// <summary>
/// 追蹤合規性規範來源的驗證狀態。
/// </summary>
public enum OdfProfileVerificationStatus
{
    /// <summary>
    /// 規範由直接驗證的官方來源支持。
    /// </summary>
    VerifiedOfficial,

    /// <summary>
    /// 規範由官方但間接的來源支持。
    /// </summary>
    OfficialButIndirect,

    /// <summary>
    /// 規範需要有效的官方來源支持才能轉為標準規範。
    /// </summary>
    NeedsActiveSource,

    /// <summary>
    /// 規範僅屬相容性規範，非正式標準。
    /// </summary>
    CompatibilityOnly
}

/// <summary>
/// 表示受支援的 ODF 版本範圍（包含邊界值）。
/// </summary>
/// <param name="minimum">範圍的最小 ODF 版本</param>
/// <param name="maximum">範圍的最大 ODF 版本</param>
public sealed class OdfVersionRange(OdfVersion minimum, OdfVersion maximum)
{
    /// <summary>
    /// 取得受支援的最小 ODF 版本。
    /// </summary>
    public OdfVersion Minimum { get; } = minimum;

    /// <summary>
    /// 取得受支援的最大 ODF 版本。
    /// </summary>
    public OdfVersion Maximum { get; } = maximum;

    /// <summary>
    /// 取得此程式庫所知的所有 ODF 版本範圍。
    /// </summary>
    public static OdfVersionRange AllKnown { get; } = new(OdfVersion.Odf10, OdfVersion.Odf14);

    /// <summary>
    /// 建立僅包含單一特定 ODF 版本的範圍。
    /// </summary>
    /// <param name="version">目標 ODF 版本</param>
    /// <returns>僅包含該版本的 OdfVersionRange 執行個體</returns>
    public static OdfVersionRange Exact(OdfVersion version) => new(version, version);

    /// <summary>
    /// 判斷指定的 ODF 版本是否在此範圍內。
    /// </summary>
    /// <param name="version">要判斷的 ODF 版本</param>
    /// <returns>若指定的版本在範圍內則為 true；否則為 false</returns>
    public bool Contains(OdfVersion version)
    {
        if (version == OdfVersion.Unknown)
        {
            return false;
        }

        return version >= Minimum && version <= Maximum;
    }
}

/// <summary>
/// 描述合規性規範所宣告的原則或驗證規則。
/// </summary>
/// <param name="id">規則的唯一識別碼</param>
/// <param name="description">規則的說明內容</param>
/// <param name="defaultSeverity">規則回報問題時的預設嚴重性</param>
public sealed class OdfPolicyRule(string id, string description, OdfIssueSeverity defaultSeverity = OdfIssueSeverity.Warning)
{
    /// <summary>
    /// 取得穩定的規則唯一識別碼。
    /// </summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentException("規則識別碼不得為空", nameof(id));

    /// <summary>
    /// 取得規則的詳細說明。
    /// </summary>
    public string Description { get; } = description ?? string.Empty;

    /// <summary>
    /// 取得規則發生問題時的預設嚴重性等級。
    /// </summary>
    public OdfIssueSeverity DefaultSeverity { get; } = defaultSeverity;
}

/// <summary>
/// 描述 ODF 合規性規範及其政策中繼資料。
/// </summary>
/// <param name="id">規範的唯一識別碼</param>
/// <param name="jurisdiction">此規範適用的管轄區域或範圍</param>
/// <param name="authority">此規範的制訂或權威機構名稱</param>
/// <param name="sourceUrl">官方來源連結的網址</param>
/// <param name="sourceDate">來源發布日期，採用 ISO 日期格式字串</param>
/// <param name="authorityLevel">規範的權威等級</param>
/// <param name="verificationStatus">來源的驗證狀態</param>
/// <param name="supportedVersions">此規範所支援的 ODF 版本範圍</param>
/// <param name="allowedExtensions">允許使用的檔案副檔名清單</param>
/// <param name="allowedMimeTypes">允許使用的 MIME 類型清單</param>
/// <param name="rules">此規範包含的合規性規則清單</param>
public sealed class OdfComplianceProfile(
    string id,
    string jurisdiction,
    string authority,
    Uri? sourceUrl,
    string? sourceDate,
    OdfPolicyAuthorityLevel authorityLevel,
    OdfProfileVerificationStatus verificationStatus,
    OdfVersionRange supportedVersions,
    IEnumerable<string> allowedExtensions,
    IEnumerable<string> allowedMimeTypes,
    IEnumerable<OdfPolicyRule> rules)
{
    /// <summary>
    /// 取得穩定的規範唯一識別碼。
    /// </summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentException("規範識別碼不得為空", nameof(id));

    /// <summary>
    /// 取得此規範適用的管轄區域。
    /// </summary>
    public string Jurisdiction { get; } = jurisdiction ?? string.Empty;

    /// <summary>
    /// 取得此規範的權威機構名稱。
    /// </summary>
    public string Authority { get; } = authority ?? string.Empty;

    /// <summary>
    /// 取得此規範的官方來源網址。
    /// </summary>
    public Uri? SourceUrl { get; } = sourceUrl;

    /// <summary>
    /// 取得來源發布日期。
    /// </summary>
    public string? SourceDate { get; } = sourceDate;

    /// <summary>
    /// 取得規範的權威等級。
    /// </summary>
    public OdfPolicyAuthorityLevel AuthorityLevel { get; } = authorityLevel;

    /// <summary>
    /// 取得來源驗證狀態。
    /// </summary>
    public OdfProfileVerificationStatus VerificationStatus { get; } = verificationStatus;

    /// <summary>
    /// 取得此規範支援的 ODF 版本範圍。
    /// </summary>
    public OdfVersionRange SupportedVersions { get; } = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));

    /// <summary>
    /// 取得允許使用的副檔名唯讀清單。
    /// </summary>
    public IReadOnlyList<string> AllowedExtensions { get; } = new List<string>(allowedExtensions ?? Array.Empty<string>()).AsReadOnly();

    /// <summary>
    /// 取得允許使用的 MIME 類型唯讀清單。
    /// </summary>
    public IReadOnlyList<string> AllowedMimeTypes { get; } = new List<string>(allowedMimeTypes ?? Array.Empty<string>()).AsReadOnly();

    /// <summary>
    /// 取得此規範所包含的合規性規則唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfPolicyRule> Rules { get; } = new List<OdfPolicyRule>(rules ?? Array.Empty<OdfPolicyRule>()).AsReadOnly();
}
