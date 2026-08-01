using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
using OdfKit.Core;

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

    /// <summary>
    /// Converts async.
    /// 轉換 Async。
    /// </summary>
    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(input, nameof(input));
        if (string.IsNullOrEmpty(inputExtension))
            throw new ArgumentNullException(nameof(inputExtension));
        if (string.IsNullOrEmpty(convertTo))
            throw new ArgumentNullException(nameof(convertTo));

        // 重試需要可重播來源；使用刪除即關閉的暫存檔，避免大型文件同時存在
        // MemoryStream 與 ToArray 複本而造成數 GB 的常駐配置。
        string inputPath = CreateTemporaryPath();
        try
        {
            using (var buffer = CreateTemporaryStream(inputPath, deleteOnClose: false))
            {
                if (input.CanSeek)
                {
                    input.Position = 0;
                }

                await CopyToBoundedAsync(input, buffer, MaxRequestBytes, "Err_UnoserverRestBackend_RequestSizeLimitExceeded", ct)
                    .ConfigureAwait(false);
            }

            int delayMs = 1000;

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await SendRequestAsync(inputPath, inputExtension, convertTo, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < 3 && (ex is HttpRequestException || ex is TaskCanceledException))
                {
                    await Task.Delay(delayMs * attempt, ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            TryDeleteTemporaryFile(inputPath, File.Delete);
        }
    }

    internal static void TryDeleteTemporaryFile(string path, Action<string> deleteFile)
    {
        try
        {
            deleteFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            OdfKitDiagnostics.Warn($"無法刪除 unoserver 輸入暫存檔：{ex.Message}", ex);
        }
    }

    private async Task<Stream> SendRequestAsync(string inputPath, string inputExtension, string convertTo, CancellationToken ct)
    {
        using var requestContent = new MultipartFormDataContent();

        var fileContent = new StreamContent(new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
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

        long? responseLength = response.Content.Headers.ContentLength;
        if (responseLength.HasValue && responseLength.Value > MaxResponseBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_UnoserverRestBackend_ResponseSizeLimitExceeded", responseLength.Value, MaxResponseBytes));
        }

        string responsePath = CreateTemporaryPath();
        FileStream result = CreateTemporaryStream(responsePath, deleteOnClose: true);
        try
        {
            using (var responseStream = await OdfKit.Internal.OdfAsyncHelper.ReadAsStreamAsync(response.Content, ct).ConfigureAwait(false))
            {
                await CopyToBoundedAsync(responseStream, result, MaxResponseBytes, "Err_UnoserverRestBackend_ResponseSizeLimitExceeded", ct)
                    .ConfigureAwait(false);
            }
            result.Position = 0;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"odfkit-unoserver-{Guid.NewGuid():N}.tmp");

    private static FileStream CreateTemporaryStream(string path, bool deleteOnClose) => new(
        path,
        FileMode.CreateNew,
        FileAccess.ReadWrite,
        FileShare.Read,
        81920,
        FileOptions.Asynchronous | FileOptions.SequentialScan |
        (deleteOnClose ? FileOptions.DeleteOnClose : FileOptions.None));

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
        while ((bytesRead = await OdfKit.Internal.OdfStreamHelper.ReadAsync(source, buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes = OdfBoundedStreamReader.AddBytes(
                totalBytes,
                bytesRead,
                maxBytes,
                (exceeded, max) => new InvalidDataException(OdfLocalizer.GetMessage(errorMessageKey, exceeded, max)));

            await OdfKit.Internal.OdfStreamHelper.WriteAsync(destination, buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
        }
    }
}
