using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// Runs LibreOffice conversions through an unoserver REST backend.
/// 實作基於 <c>unoserver-rest-api</c> 的雲端 LibreOffice 轉檔後端。
/// </summary>
public sealed class UnoserverRestBackend : ILibreOfficeConversionBackend
{
    private const long MaxRequestBytes = 1024L * 1024 * 1024;
    private const long MaxResponseBytes = 1024L * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    /// <summary>
    /// Runs LibreOffice conversions through an unoserver REST backend.
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

        // 先將輸入完整緩衝為位元組陣列：重試需要可重複讀取的來源，
        // 且不可依賴呼叫端傳入的串流是否可尋覽。每次嘗試以此緩衝建立新的
        // 內容，避免 MultipartFormDataContent 釋放時連帶關閉呼叫端的串流，
        // 導致下一次重試擲出 ObjectDisposedException。
        byte[] inputBytes;
        using (var buffer = new MemoryStream())
        {
            if (input.CanSeek)
            {
                input.Position = 0;
            }

            await CopyToBoundedAsync(input, buffer, MaxRequestBytes, "Err_UnoserverRestBackend_RequestSizeLimitExceeded", ct)
                .ConfigureAwait(false);
            inputBytes = buffer.ToArray();
        }

        int maxRetries = 3;
        int delayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await SendRequestAsync(inputBytes, inputExtension, convertTo, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException || ex is TaskCanceledException))
            {
                // Polly 風格之重試與指數型延遲
                await Task.Delay(delayMs * attempt, ct).ConfigureAwait(false);
            }
        }

        // 最後一次重試直接拋出例外
        return await SendRequestAsync(inputBytes, inputExtension, convertTo, ct).ConfigureAwait(false);
    }

    private async Task<Stream> SendRequestAsync(byte[] inputBytes, string inputExtension, string convertTo, CancellationToken ct)
    {
        using var requestContent = new MultipartFormDataContent();

        // 每次嘗試都以緩衝位元組建立全新的 ByteArrayContent，
        // 使重試彼此獨立，且不觸及呼叫端原始串流的生命週期。
        var fileContent = new ByteArrayContent(inputBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        // unoserver-rest-api 要求 file 欄位名稱必須是 "file"，並包含副檔名 filename
        requestContent.Add(fileContent, "file", $"document.{inputExtension}");

        var convertToContent = new StringContent(convertTo);
        requestContent.Add(convertToContent, "convert-to");

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = requestContent
        };

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // 讀取為 MemoryStream，防止與原網路連線生命週期強綁定，導致呼叫端讀取時連線已關閉。
        long? responseLength = response.Content.Headers.ContentLength;
        if (responseLength.HasValue && responseLength.Value > MaxResponseBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_UnoserverRestBackend_ResponseSizeLimitExceeded", responseLength.Value, MaxResponseBytes));
        }

        var ms = new MemoryStream(responseLength.HasValue && responseLength.Value <= int.MaxValue
            ? (int)responseLength.Value
            : 0);
        using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        {
            await CopyToBoundedAsync(responseStream, ms, MaxResponseBytes, "Err_UnoserverRestBackend_ResponseSizeLimitExceeded", ct)
                .ConfigureAwait(false);
        }
        ms.Position = 0;
        return ms;
    }

    private static async Task CopyToBoundedAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        string errorMessageKey,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (maxBytes > 0 && bytesRead > maxBytes - totalBytes)
            {
                long exceededBytes = totalBytes > long.MaxValue - bytesRead
                    ? long.MaxValue
                    : totalBytes + bytesRead;
                throw new InvalidDataException(OdfLocalizer.GetMessage(errorMessageKey, exceededBytes, maxBytes));
            }

            totalBytes += bytesRead;
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
        }
    }
}
