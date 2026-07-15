using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Registers secure WebFont asset services and endpoints.
/// 註冊安全的 WebFont 資產服務與 endpoint。
/// </summary>
public static class OdfWebFontEndpointExtensions
{
    private const string DefaultRoutePrefix = "/_odf-fonts";

    /// <summary>
    /// Adds the bounded, read-only WebFont asset store.
    /// 加入有界且唯讀的 WebFont 資產儲存區。
    /// </summary>
    /// <param name="services">The application service collection. / 應用程式服務集合。</param>
    /// <param name="configure">The trusted asset-store configuration delegate. / 受信任資產儲存區的設定委派。</param>
    /// <returns>The original service collection. / 原始服務集合。</returns>
    public static IServiceCollection AddOdfWebFonts(
        this IServiceCollection services,
        Action<OdfWebFontOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        services.AddSingleton<WebFontAssetStore>();
        services.AddSingleton(serviceProvider => new OdfWebFontResourceProvider(
            serviceProvider.GetRequiredService<IOptions<OdfWebFontOptions>>(),
            serviceProvider.GetRequiredService<WebFontAssetStore>()));
        return services;
    }

    /// <summary>
    /// Adds a bounded, read-only WebFont asset store using secure defaults.
    /// 使用安全預設值加入有界且唯讀的 WebFont 資產儲存區。
    /// </summary>
    /// <param name="services">The application service collection. / 應用程式服務集合。</param>
    /// <param name="assetRootPath">The trusted generated-asset directory. / 受信任的產生資產目錄。</param>
    /// <returns>The original service collection. / 原始服務集合。</returns>
    public static IServiceCollection AddOdfWebFonts(
        this IServiceCollection services,
        string assetRootPath)
        => AddOdfWebFonts(services, options => options.AssetRootPath = assetRootPath);

    /// <summary>
    /// Maps the WebFont manifest and immutable asset endpoints at the default route prefix.
    /// 在預設路由前綴對應 WebFont manifest 與不可變資產 endpoint。
    /// </summary>
    /// <param name="endpoints">The endpoint route builder. / endpoint 路由建構器。</param>
    /// <returns>The mapped route group. / 已對應的路由群組。</returns>
    public static RouteGroupBuilder MapOdfWebFonts(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        OdfWebFontOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<OdfWebFontOptions>>()
            .Value;
        return MapOdfWebFonts(endpoints, options.RoutePrefix);
    }

    /// <summary>
    /// Maps the WebFont manifest and immutable asset endpoints at a route prefix.
    /// 在指定路由前綴對應 WebFont manifest 與不可變資產 endpoint。
    /// </summary>
    /// <param name="endpoints">The endpoint route builder. / endpoint 路由建構器。</param>
    /// <param name="routePrefix">The application-relative route prefix. / 應用程式相對路由前綴。</param>
    /// <returns>The mapped route group. / 已對應的路由群組。</returns>
    public static RouteGroupBuilder MapOdfWebFonts(
        this IEndpointRouteBuilder endpoints,
        string routePrefix)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        OdfWebFontOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<OdfWebFontOptions>>()
            .Value;
        OdfWebFontOptionValidator.Validate(options);
        _ = endpoints.ServiceProvider.GetRequiredService<WebFontAssetStore>();

        RouteGroupBuilder group = endpoints.MapGroup(routePrefix);
        group.MapGet(
            "/manifest.json",
            static (HttpContext context, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.Json(store.Manifest);
            });
        group.MapGet(
            "/webfonts.css",
            static (HttpContext context, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                if (store.Css is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.Text(store.Css, "text/css; charset=utf-8");
            });
        group.MapGet(
            "/{stylesheetFileName}",
            static (HttpContext context, string stylesheetFileName, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                if (!store.IsStylesheet(stylesheetFileName)
                    || store.Css is null
                    || store.StylesheetSha256 is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                context.Response.Headers.ETag = $"\"{store.StylesheetSha256}\"";
                return Results.Text(store.Css, "text/css; charset=utf-8");
            });
        group.MapGet(
            "/{sha256:regex(^[a-fA-F0-9]{{64}}$)}/{fileName}",
            static (HttpContext context, string sha256, string fileName, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                if (!store.TryGetAsset(sha256, fileName, out StoredWebFontAsset? asset)
                    || asset is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.File(
                    asset.FullPath,
                    asset.ContentType,
                    lastModified: asset.LastModified,
                    entityTag: new EntityTagHeaderValue($"\"{asset.Descriptor.Sha256}\""),
                    enableRangeProcessing: false);
            });
        return group;
    }

    private static void ApplyCrossOriginHeaders(HttpContext context)
    {
        OdfWebFontOptions options = context.RequestServices
            .GetRequiredService<IOptions<OdfWebFontOptions>>()
            .Value;
        context.Response.Headers["Cross-Origin-Resource-Policy"] = options.CrossOriginResourcePolicy switch
        {
            OdfWebFontCrossOriginPolicy.SameOrigin => "same-origin",
            OdfWebFontCrossOriginPolicy.SameSite => "same-site",
            OdfWebFontCrossOriginPolicy.CrossOrigin => "cross-origin",
            _ => "same-origin"
        };

        string origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && OdfWebFontOptionValidator.IsAllowedOrigin(options, origin))
        {
            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.AppendCommaSeparatedValues(HeaderNames.Vary, HeaderNames.Origin);
        }
    }
}
