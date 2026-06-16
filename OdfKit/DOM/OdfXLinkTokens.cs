namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>xlink:type</c> 的 XLink 類型 token。
/// </summary>
public enum OdfXLinkType
{
    /// <summary>
    /// 簡單連結。
    /// </summary>
    Simple
}

/// <summary>
/// 表示 ODF schema 中 <c>xlink:show</c> 的 XLink 顯示行為 token。
/// </summary>
public enum OdfXLinkShow
{
    /// <summary>
    /// 將連結資源嵌入目前內容。
    /// </summary>
    Embed,

    /// <summary>
    /// 在新的瀏覽或呈現環境中顯示連結資源。
    /// </summary>
    New,

    /// <summary>
    /// 不指定顯示行為。
    /// </summary>
    None,

    /// <summary>
    /// 取代目前瀏覽或呈現環境。
    /// </summary>
    Replace
}

/// <summary>
/// 表示 ODF schema 中 <c>xlink:actuate</c> 的 XLink 觸發行為 token。
/// </summary>
public enum OdfXLinkActuate
{
    /// <summary>
    /// 載入資源時自動觸發。
    /// </summary>
    OnLoad,

    /// <summary>
    /// 依使用者或應用程式要求觸發。
    /// </summary>
    OnRequest
}
