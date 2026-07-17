using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OdfKit.WebFonts.Worker;

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
    /// Adds an opt-in bounded WebFont generation worker for authenticated endpoints.
    /// 加入選擇啟用且供授權 endpoint 使用的有界 WebFont 動態產生 worker。
    /// </summary>
    /// <param name="services">The application service collection. / 應用程式服務集合。</param>
    /// <param name="engineFactory">The factory for an isolated trusted subset engine adapter. / 建立隔離且受信任子集引擎 adapter 的工廠。</param>
    /// <param name="configureGeneration">The generation endpoint allowlist and limits. / 動態產生 endpoint 的允許清單與限制。</param>
    /// <param name="configureWorker">The bounded worker limits and durable-cache settings. / 有界 worker 限制與耐久快取設定。</param>
    /// <returns>The original service collection. / 原始服務集合。</returns>
    public static IServiceCollection AddOdfWebFontGeneration(
        this IServiceCollection services,
        Func<IServiceProvider, IWebFontSubsetEngine> engineFactory,
        Action<OdfWebFontGenerationOptions> configureGeneration,
        Action<WebFontWorkerOptions> configureWorker)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(engineFactory);
        ArgumentNullException.ThrowIfNull(configureGeneration);
        ArgumentNullException.ThrowIfNull(configureWorker);
        services.Configure(configureGeneration);
        services.Configure(configureWorker);
        services.PostConfigure<OdfWebFontOptions>(
            options => options.AllowMissingManifestForGeneration = true);
        services.AddSingleton(serviceProvider => new WebFontGenerationWorker(
            engineFactory(serviceProvider)
                ?? throw new InvalidOperationException(
                    OdfKit.Compliance.OdfLocalizer.GetMessage("Err_WebFont_ConfigurationInvalid")),
            serviceProvider.GetRequiredService<IOptions<WebFontWorkerOptions>>().Value));
        services.AddSingleton<OdfWebFontGenerationService>();
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
        group.MapMethods(
            "/manifest.json",
            [HttpMethods.Get, HttpMethods.Head],
            static (HttpContext context, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.File(
                    store.ManifestBytes,
                    "application/json; charset=utf-8",
                    entityTag: new EntityTagHeaderValue($"\"{store.ManifestSha256}\""));
            });
        group.MapMethods(
            "/webfonts.css",
            [HttpMethods.Get, HttpMethods.Head],
            static (HttpContext context, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                if (store.CssBytes is null || store.CssSha256 is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.File(
                    store.CssBytes,
                    "text/css; charset=utf-8",
                    entityTag: new EntityTagHeaderValue($"\"{store.CssSha256}\""));
            });
        group.MapMethods(
            "/{stylesheetFileName}",
            [HttpMethods.Get, HttpMethods.Head],
            static (HttpContext context, string stylesheetFileName, WebFontAssetStore store) =>
            {
                ApplyCrossOriginHeaders(context);
                if (!store.IsStylesheet(stylesheetFileName)
                    || store.CssBytes is null
                    || store.CssSha256 is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                return Results.File(
                    store.CssBytes,
                    "text/css; charset=utf-8",
                    entityTag: new EntityTagHeaderValue($"\"{store.CssSha256}\""));
            });
        group.MapMethods(
            "/{sha256:regex(^[a-fA-F0-9]{{64}}$)}/{fileName}",
            [HttpMethods.Get, HttpMethods.Head],
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

        OdfWebFontGenerationService? generationService = endpoints.ServiceProvider
            .GetService<OdfWebFontGenerationService>();
        if (generationService is not null)
        {
            OdfWebFontGenerationOptions generationOptions = endpoints.ServiceProvider
                .GetRequiredService<IOptions<OdfWebFontGenerationOptions>>()
                .Value;
            OdfWebFontGenerationOptionValidator.Validate(generationOptions);
            group.MapPost(
                    "/generate",
                    static (HttpRequest request, OdfWebFontGenerationService service, CancellationToken cancellationToken)
                        => service.GenerateAsync(request, cancellationToken))
                .RequireAuthorization(new AuthorizeAttribute
                {
                    Policy = generationOptions.AuthorizationPolicyName
                })
                .RequireRateLimiting(generationOptions.RateLimiterPolicyName);
        }

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
