namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Defines the Cross-Origin-Resource-Policy value emitted for locally hosted WebFont assets.
/// 定義本機託管 WebFont 資產輸出的 Cross-Origin-Resource-Policy 值。
/// </summary>
public enum OdfWebFontCrossOriginPolicy
{
    /// <summary>
    /// Restricts resource use to the same origin.
    /// 將資源使用限制於相同來源。
    /// </summary>
    SameOrigin,

    /// <summary>
    /// Restricts resource use to the same site.
    /// 將資源使用限制於相同站台。
    /// </summary>
    SameSite,

    /// <summary>
    /// Allows resource use across origins while CORS remains independently enforced.
    /// 允許跨來源使用資源，且仍由 CORS 獨立執行限制。
    /// </summary>
    CrossOrigin
}
