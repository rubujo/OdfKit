namespace OdfKit.WebFonts.Build;

/// <summary>
/// Defines the CSS font-display strategy emitted for generated WebFont faces.
/// 定義產生 WebFont face 時輸出的 CSS font-display 策略。
/// </summary>
public enum WebFontDisplayMode
{
    /// <summary>
    /// Uses the browser default strategy.
    /// 使用瀏覽器預設策略。
    /// </summary>
    Auto,

    /// <summary>
    /// Uses a short blocking period followed by an unlimited swap period.
    /// 使用短暫阻擋期間，後接無限期替換期間。
    /// </summary>
    Block,

    /// <summary>
    /// Displays fallback text immediately and swaps when the WebFont arrives.
    /// 立即顯示 fallback 文字，並在 WebFont 抵達時替換。
    /// </summary>
    Swap,

    /// <summary>
    /// Uses a short block and a limited swap period.
    /// 使用短暫阻擋與有限替換期間。
    /// </summary>
    Fallback,

    /// <summary>
    /// Uses the WebFont only when it is available during the initial rendering opportunity.
    /// 僅在初次呈現時機可取得 WebFont 時使用該字型。
    /// </summary>
    Optional
}
