<%@ WebHandler Language="C#" Class="WebFontGenerateProxy" %>

using System;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

/// <summary>
/// Proxies an authenticated sample request to the private WebFont generation endpoint.
/// 將已驗證的範例要求轉送至私有 WebFont 產生端點。
/// </summary>
public sealed class WebFontGenerateProxy : HttpTaskAsyncHandler
{
    private const int MaximumBodyBytes = 64 * 1024;
    private static readonly HttpClient Client = new HttpClient(
        new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
        })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Gets whether the handler instance can be reused.
    /// 取得是否可重複使用處理常式執行個體。
    /// </summary>
    public override bool IsReusable
    {
        get { return true; }
    }

    /// <summary>
    /// Proxies one bounded JSON request without exposing the API key to the browser.
    /// 代理單一有界 JSON 要求，且不向瀏覽器公開 API key。
    /// </summary>
    /// <param name="context">The current HTTP context. / 目前的 HTTP 內容。</param>
    /// <returns>A task representing the proxy operation. / 代表代理作業的工作。</returns>
    public override async Task ProcessRequestAsync(HttpContext context)
    {
        string contentType = context.Request.ContentType ?? string.Empty;
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.Ordinal)
            || (!string.Equals(
                    contentType,
                    "application/json",
                    StringComparison.OrdinalIgnoreCase)
                && !contentType.StartsWith(
                    "application/json;",
                    StringComparison.OrdinalIgnoreCase))
            || context.Request.ContentLength < 0
            || context.Request.ContentLength > MaximumBodyBytes)
        {
            context.Response.StatusCode = 400;
            context.Response.AppendHeader("X-OdfKit-Sample-Error", "InvalidRequest");
            return;
        }

        string endpointValue = ConfigurationManager.AppSettings[
            "OdfKit.WebFonts.SampleInternalGenerateUrl"];
        Uri endpoint;
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out endpoint)
            || !endpoint.IsLoopback
            || !endpoint.AbsolutePath.EndsWith(
                "/_odf-fonts/generate",
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = 500;
            context.Response.AppendHeader("X-OdfKit-Sample-Error", "InvalidInternalEndpoint");
            return;
        }

        string apiKey = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_API_KEY")
            ?? ConfigurationManager.AppSettings["OdfKit.WebFonts.ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = 500;
            context.Response.AppendHeader("X-OdfKit-Sample-Error", "MissingApiKey");
            return;
        }

        string backend = context.Request.Headers["X-OdfKit-WebFont-Backend"];
        if (!string.Equals(backend, "sidecar", StringComparison.Ordinal)
            && !string.Equals(backend, "managed", StringComparison.Ordinal))
        {
            context.Response.StatusCode = 400;
            context.Response.AppendHeader("X-OdfKit-Sample-Error", "InvalidBackend");
            return;
        }

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.TryAddWithoutValidation("X-OdfKit-WebFont-Key", apiKey);
                if (string.Equals(backend, "managed", StringComparison.Ordinal))
                {
                    request.Headers.TryAddWithoutValidation("X-OdfKit-WebFont-Backend", "managed");
                }

                request.Content = new StreamContent(context.Request.InputStream);
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                using (HttpResponseMessage response =
                    await Client.SendAsync(request).ConfigureAwait(false))
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType =
                        response.Content.Headers.ContentType == null
                            ? "application/json"
                            : response.Content.Headers.ContentType.ToString();
                    context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                    context.Response.Cache.SetNoStore();
                    if (response.Headers.RetryAfter != null)
                    {
                        context.Response.AppendHeader(
                            "Retry-After",
                            response.Headers.RetryAfter.ToString());
                    }

                    await response.Content.CopyToAsync(context.Response.OutputStream)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (HttpRequestException exception)
        {
            context.Trace.Warn(
                "OdfKit.WebFonts.Sample",
                "The private WebFont endpoint request failed.",
                exception);
            context.Response.StatusCode = 502;
            context.Response.AppendHeader("X-OdfKit-Sample-Error", "InternalEndpointUnavailable");
        }
    }
}
