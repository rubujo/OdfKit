namespace OdfKit.Compliance;

/// <summary>
/// Defines practical editing and interoperability profiles.
/// 定義實務編輯與互通性設定檔。
/// </summary>
public enum OdfPracticalCompatibilityProfile
{
    /// <summary>
    /// Checks risks for the current LibreOffice generation.
    /// 檢查目前 LibreOffice 世代的風險。
    /// </summary>
    LibreOfficeCurrent,

    /// <summary>
    /// Checks risks when files are expected to be opened or edited in Microsoft Office.
    /// 檢查文件預期由 Microsoft Office 開啟或編輯時的風險。
    /// </summary>
    MicrosoftOfficeOdf,

    /// <summary>
    /// Checks risks for portable editing across multiple office suites.
    /// 檢查跨多套辦公軟體可攜編輯的風險。
    /// </summary>
    PortableEditing
}
