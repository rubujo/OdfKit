using OdfKit.DOM;

namespace OdfKit.Core;
/// <summary>
/// Adds macro sanitization helpers for ODF packages.
/// 提供 ODF 封裝的巨集淨化輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    #region Macro Sanitization


    /// <summary>
    /// Executes the SanitizeMacros operation.
    /// 淨化封裝以移除所有 VBA、StarBasic 巨集指令碼、簽章以及指令碼參考。
    /// </summary>
    public void SanitizeMacros()
    {
        _lock.Wait();
        try
        {
            OdfPackageMacroSanitizer.Sanitize(MacroSanitizeCollaborators);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Executes the SanitizeXmlNode operation.
    /// 遞迴淨化指定的 XML 節點，移除事件監聽器與巨集或指令碼屬性。
    /// </summary>
    /// <param name="node">要淨化的 ODF 節點</param>
    /// <returns>若節點被修改則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public static bool SanitizeXmlNode(OdfNode node)
        => OdfPackageXmlMacroSanitizer.SanitizeNode(node);


    #endregion

    #region ZIP Path & Entry Sanitize (Zip Slip Protection)


    /// <summary>
    /// Executes the SanitizeEntryName operation.
    /// 淨化與驗證 ZIP 專案名稱，防止目錄穿越攻擊（Zip Slip 漏洞防禦）。
    /// </summary>
    /// <param name="name">原始專案名稱</param>
    /// <returns>淨化後的標準專案名稱</returns>
    public static string SanitizeEntryName(string name)
        => OdfPackageEntryNameSanitizer.Sanitize(name);


    #endregion

}
