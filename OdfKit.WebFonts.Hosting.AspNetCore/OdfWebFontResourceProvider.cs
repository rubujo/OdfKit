using System.Net;
using Microsoft.Extensions.Options;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Produces CSP-friendly URLs and markup for prebuilt WebFont resources.
/// 產生符合 CSP 的預建 WebFont 資源 URL 與 markup。
/// </summary>
public sealed class OdfWebFontResourceProvider
{
    private readonly string _publicBaseUrl;

    /// <summary>
    /// Initializes a WebFont resource provider from validated hosting options.
    /// 從已驗證的託管設定初始化 WebFont 資源 provider。
    /// </summary>
    /// <param name="optionsAccessor">The registered hosting options. / 已註冊的託管設定。</param>
    /// <param name="assetStore">The validated local asset catalog. / 已驗證的本機資產目錄。</param>
    internal OdfWebFontResourceProvider(
        IOptions<OdfWebFontOptions> optionsAccessor,
        WebFontAssetStore assetStore)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        OdfWebFontOptions options = optionsAccessor.Value;
        OdfWebFontOptionValidator.Validate(options);
        _publicBaseUrl = ResolvePublicBaseUrl(options);
        StylesheetFileName = assetStore.StylesheetFileName ?? "webfonts.css";
    }

    /// <summary>
    /// Gets the content-fingerprinted stylesheet file name when available.
    /// 取得可用的內容指紋樣式表檔名。
    /// </summary>
    public string StylesheetFileName { get; }

    /// <summary>
    /// Gets the absolute or application-relative stylesheet URL.
    /// 取得絕對或應用程式相對的樣式表 URL。
    /// </summary>
    public string StylesheetUrl => string.Concat(_publicBaseUrl, "/", StylesheetFileName);

    /// <summary>
    /// Gets the CSP source expression required by both style-src and font-src.
    /// 取得 style-src 與 font-src 皆需要的 CSP 來源運算式。
    /// </summary>
    public string ContentSecurityPolicySource
    {
        get
        {
            if (!Uri.TryCreate(_publicBaseUrl, UriKind.Absolute, out Uri? uri))
            {
                return "'self'";
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }
    }

    /// <summary>
    /// Creates an encoded external stylesheet link without inline script or style content.
    /// 建立不含行內 script 或 style 內容的編碼外部樣式表連結。
    /// </summary>
    /// <returns>The HTML link element. / HTML link 元素。</returns>
    public string CreateStylesheetLink()
        => $"<link rel=\"stylesheet\" href=\"{WebUtility.HtmlEncode(StylesheetUrl)}\" />";

    private static string ResolvePublicBaseUrl(OdfWebFontOptions options)
    {
        string value = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? options.RoutePrefix
            : options.PublicBaseUrl;
        return value.TrimEnd('/');
    }
}
