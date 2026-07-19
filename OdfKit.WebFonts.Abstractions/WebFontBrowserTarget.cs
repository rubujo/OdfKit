namespace OdfKit.WebFonts;

/// <summary>
/// Identifies a browser engine that must render a generated WebFont asset.
/// 識別必須能呈現產生之 WebFont 資產的瀏覽器引擎。
/// </summary>
public enum WebFontBrowserTarget
{
    /// <summary>
    /// Targets the Chromium browser engine.
    /// 以 Chromium 瀏覽器引擎為目標。
    /// </summary>
    Chromium,

    /// <summary>
    /// Targets the Firefox browser engine.
    /// 以 Firefox 瀏覽器引擎為目標。
    /// </summary>
    Firefox,

    /// <summary>
    /// Targets the WebKit browser engine exercised by Playwright.
    /// 以 Playwright 驗證的 WebKit 瀏覽器引擎為目標。
    /// </summary>
    WebKit
}
