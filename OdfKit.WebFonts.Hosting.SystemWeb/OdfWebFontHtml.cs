using System.Configuration;
using System.Web;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Hosting.SystemWeb;

/// <summary>
/// Creates Web Forms markup for prebuilt WebFont CSS.
/// 建立預產生 WebFont CSS 的 Web Forms markup。
/// </summary>
public static class OdfWebFontHtml
{
    /// <summary>
    /// Creates a stylesheet link for the default WebFont route.
    /// 建立預設 WebFont 路由的樣式表連結。
    /// </summary>
    /// <returns>HTML-safe link markup. / HTML 安全的連結 markup。</returns>
    public static IHtmlString StylesheetLink()
    {
        string baseUrl = ConfigurationManager.AppSettings["OdfKit.WebFonts.PublicBaseUrl"]
            ?? "/_odf-fonts";
        string fileName = ConfigurationManager.AppSettings["OdfKit.WebFonts.StylesheetFileName"]
            ?? "webfonts.css";
        if (!IsSafeBaseUrl(baseUrl) || !IsPlainFileName(fileName))
        {
            throw new ConfigurationErrorsException(
                OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid"));
        }

        return StylesheetLink($"{baseUrl.TrimEnd('/')}/{fileName}");
    }

    /// <summary>
    /// Creates a stylesheet link for a specified application-relative URL.
    /// 建立指定應用程式相對 URL 的樣式表連結。
    /// </summary>
    /// <param name="stylesheetUrl">The application-relative or absolute HTTP(S) stylesheet URL. / 應用程式相對或絕對 HTTP(S) 樣式表 URL。</param>
    /// <returns>HTML-safe link markup. / HTML 安全的連結 markup。</returns>
    /// <exception cref="ArgumentException">The URL is empty, protocol-relative, or not HTTP(S). / URL 為空、為協定相對或非 HTTP(S)。</exception>
    public static IHtmlString StylesheetLink(string stylesheetUrl)
    {
        // 與無參數多載套用相同的來源限制：協定相對 URL（//evil.com/x.css）會從
        // 攻擊者來源載入樣式表，而外部 CSS 可經由屬性選擇器外洩頁面資料。
        // 先前此多載完全不驗證，與同類的無參數多載不一致。
        if (string.IsNullOrWhiteSpace(stylesheetUrl) || !IsSafeBaseUrl(stylesheetUrl))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"),
                nameof(stylesheetUrl));
        }

        string encoded = HttpUtility.HtmlAttributeEncode(stylesheetUrl);
        return new HtmlString($"<link rel=\"stylesheet\" href=\"{encoded}\" />");
    }

    private static bool IsSafeBaseUrl(string value)
    {
        if (global::OdfKit.Internal.OdfStringHelper.StartsWith(value, '/')
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Contains('\\')
            && !value.Contains('?')
            && !value.Contains('#'))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https"
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsPlainFileName(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
