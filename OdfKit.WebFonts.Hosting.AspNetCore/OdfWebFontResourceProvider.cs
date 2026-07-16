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
    private readonly WebFontAssetStore _assetStore;
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
        _assetStore = assetStore;
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

    /// <summary>
    /// Creates an opt-in preload link for one validated immutable font asset.
    /// 為單一已驗證不可變字型資產建立選擇啟用的 preload link。
    /// </summary>
    /// <param name="asset">The manifest asset to preload. / 要預先載入的 manifest 資產。</param>
    /// <returns>The encoded HTML link element. / 已編碼的 HTML link 元素。</returns>
    /// <exception cref="ArgumentException">The asset is not present in the validated store. / 資產不存在於已驗證的儲存區。</exception>
    public string CreateFontPreloadLink(WebFontAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!_assetStore.TryGetAsset(asset.Sha256, asset.FileName, out StoredWebFontAsset? stored)
            || stored is null
            || stored.Descriptor.Format != asset.Format)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"),
                nameof(asset));
        }

        string url = string.Concat(_publicBaseUrl, "/", asset.Sha256, "/", asset.FileName);
        return $"<link rel=\"preload\" href=\"{WebUtility.HtmlEncode(url)}\" as=\"font\" type=\"{GetContentType(asset.Format)}\" crossorigin=\"anonymous\" />";
    }

    private static string GetContentType(WebFontFormat format)
        => format switch
        {
            WebFontFormat.Woff2 => "font/woff2",
            WebFontFormat.Woff => "font/woff",
            WebFontFormat.TrueType => "font/ttf",
            WebFontFormat.OpenType => "font/otf",
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"))
        };

    private static string ResolvePublicBaseUrl(OdfWebFontOptions options)
    {
        string value = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? options.RoutePrefix
            : options.PublicBaseUrl;
        return value.TrimEnd('/');
    }
}
