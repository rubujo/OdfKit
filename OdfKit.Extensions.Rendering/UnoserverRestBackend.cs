using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// Provides unoserver rest backend.
/// 實作基於 <c>unoserver-rest-api</c> 的雲端 LibreOffice 轉檔後端。
/// </summary>
public sealed class UnoserverRestBackend : ILibreOfficeConversionBackend
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    /// <summary>
    /// Provides unoserver rest backend.
    /// 初始化 <see cref="UnoserverRestBackend"/> 類別的新執行個體。
    /// </summary>
    /// <param name="endpoint">The numeric value. / unoserver-rest-api 轉換服務端點（例如 <c>http://localhost:2004/request</c>）</param>
    /// <param name="httpClient">The value to use. / 可選用的自訂 HttpClient 執行個體</param>
    public UnoserverRestBackend(string endpoint = "http://localhost:2004/request", HttpClient? httpClient = null)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _httpClient = httpClient ?? SharedHttpClient;
    }

    private static HttpClient CreateSharedHttpClient()
    {
#if NETSTANDARD2_0
        var handler = new HttpClientHandler
        {
            MaxConnectionsPerServer = 100
        };
#else
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 100
        };
#endif
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrEmpty(inputExtension))
            throw new ArgumentNullException(nameof(inputExtension));
        if (string.IsNullOrEmpty(convertTo))
            throw new ArgumentNullException(nameof(convertTo));

        int maxRetries = 3;
        int delayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await SendRequestAsync(input, inputExtension, convertTo, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException || ex is TaskCanceledException))
            {
                // Polly 風格之重試與指數型延遲
                await Task.Delay(delayMs * attempt, ct).ConfigureAwait(false);
            }
        }

        // 最後一次重試直接拋出例外
        return await SendRequestAsync(input, inputExtension, convertTo, ct).ConfigureAwait(false);
    }

    private async Task<Stream> SendRequestAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
    {
        // 每次發送請求都需要確保資料流的 Position 處於起點，
        // 且 multipart content 的 StreamContent 不會因為重複讀取而失敗。
        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var requestContent = new MultipartFormDataContent();

        var fileContent = new StreamContent(input);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        // unoserver-rest-api 要求 file 欄位名稱必須是 "file"，並包含副檔名 filename
        requestContent.Add(fileContent, "file", $"document.{inputExtension}");

        var convertToContent = new StringContent(convertTo);
        requestContent.Add(convertToContent, "convert-to");

        var response = await _httpClient.PostAsync(_endpoint, requestContent, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // 讀取為 MemoryStream，防止與原網路連線生命週期強綁定，導致呼叫端讀取時連線已關閉。
        var ms = new MemoryStream();
        using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        {
            await responseStream.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
        }
        ms.Position = 0;
        return ms;
    }
}
