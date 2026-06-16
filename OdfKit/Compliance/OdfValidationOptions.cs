using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 表示公開 ODF 驗證入口使用的選項。
/// </summary>
public sealed class OdfValidationOptions
{
    /// <summary>
    /// 取得預設驗證選項。
    /// </summary>
    public static OdfValidationOptions Default { get; } = new();

    /// <summary>
    /// 取得 ODF 1.4 嚴格一致性驗證選項。
    /// </summary>
    public static OdfValidationOptions Odf14Strict { get; } = new()
    {
        Profile = OdfComplianceProfiles.OasisOdf14Strict
    };

    /// <summary>
    /// 取得 ODF 1.4 擴充一致性驗證選項。
    /// </summary>
    public static OdfValidationOptions Odf14Extended { get; } = new()
    {
        Profile = OdfComplianceProfiles.OasisOdf14Extended
    };

    /// <summary>
    /// 取得驗證時使用的相容性設定檔。
    /// </summary>
    public OdfComplianceProfile? Profile { get; set; }

    /// <summary>
    /// 取得用於格式偵測與設定檔副檔名檢查的檔案名稱。
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 取得開啟封裝文件時使用的載入選項。
    /// </summary>
    public OdfLoadOptions? LoadOptions { get; set; }
}
