using System.Globalization;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// Provides the OdfValidationOptions API.
/// 表示公開 ODF 驗證入口使用的選項。
/// </summary>
/// <remarks>
/// Prefer this options object over multi-optional parameter lists on validators.
/// 驗證 API 請優先使用此 options 物件，避免多個尾端可選參數。
/// </remarks>
public sealed class OdfValidationOptions
{
    /// <summary>
    /// Gets the Default value.
    /// 取得預設驗證選項。
    /// </summary>
    public static OdfValidationOptions Default { get; } = new();

    /// <summary>
    /// Gets the Odf14Strict value.
    /// 取得 ODF 1.4 嚴格一致性驗證選項。
    /// </summary>
    public static OdfValidationOptions Odf14Strict { get; } = new()
    {
        Profile = OdfComplianceProfiles.OasisOdf14Strict
    };

    /// <summary>
    /// Gets the Odf14Extended value.
    /// 取得 ODF 1.4 擴充一致性驗證選項。
    /// </summary>
    public static OdfValidationOptions Odf14Extended { get; } = new()
    {
        Profile = OdfComplianceProfiles.OasisOdf14Extended
    };

    /// <summary>
    /// Gets or sets the Profile value.
    /// 取得或設定驗證時使用的相容性設定檔。
    /// </summary>
    public OdfComplianceProfile? Profile { get; set; }

    /// <summary>
    /// Gets or sets the FileName value.
    /// 取得或設定用於格式偵測與設定檔副檔名檢查的檔案名稱。
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the LoadOptions value.
    /// 取得或設定開啟封裝文件時使用的載入選項。
    /// </summary>
    public OdfLoadOptions? LoadOptions { get; set; }

    /// <summary>
    /// Gets or sets the culture used when generating validation issue text.
    /// 取得或設定產生驗證問題文字時使用的文化特性。
    /// </summary>
    public CultureInfo? Culture { get; set; }
}
