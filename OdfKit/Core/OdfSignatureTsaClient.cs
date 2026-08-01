using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Tsp;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// TSA 時間戳記與 CRL 下載用戶端（內部協作者）。
/// </summary>
internal static class OdfSignatureTsaClient
{
    private const long MaxCrlResponseBytes = 10 * 1024 * 1024;
    private const long MaxTsaResponseBytes = 1024 * 1024;

    // 預設逾時 30 秒，避免呼叫端未提供自訂 HttpClient 時，TSA／CRL 連線因網路異常而無限期掛起。
    // Default timeout of 30 seconds, to avoid indefinitely hanging TSA/CRL connections
    // when the caller does not supply a custom HttpClient.
    private static readonly HttpClient s_httpClient = new(
        new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

#if !NETSTANDARD2_0
    private static readonly HttpClient s_crlHttpClient = CreatePinnedCrlHttpClient();

    private static HttpClient CreatePinnedCrlHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = ConnectToPublicAddressAsync
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host,
            cancellationToken).ConfigureAwait(false);

        Exception? lastError = null;
        foreach (IPAddress address in addresses)
        {
            if (IsPrivateOrSpecialAddress(address))
                continue;

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = ex;
            }
        }

        throw new HttpRequestException(
            OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_UnsafeCrlUri"),
            lastError);
    }
#endif

    internal static async Task<byte[]> DownloadCrlAsync(
        string url,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default) =>
        await DownloadCrlAsync(url, httpClient, allowedHosts: null, cancellationToken).ConfigureAwait(false);

    internal static async Task<byte[]> DownloadCrlAsync(
        string url,
        HttpClient? httpClient,
        ISet<string>? allowedHosts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = httpClient ??
#if NETSTANDARD2_0
            s_httpClient;
#else
            s_crlHttpClient;
#endif
        Uri currentUri = httpClient is null
            ? await ValidatePublicHttpUriAsync(url, cancellationToken).ConfigureAwait(false)
            : ValidateHttpUri(url);
        ValidateAllowedHost(currentUri, allowedHosts);
        int remainingRedirects = httpClient is null ? 5 : 0;

        while (true)
        {
            using HttpResponseMessage response = await client
                .GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (remainingRedirects > 0 && IsRedirect(response.StatusCode) && response.Headers.Location is Uri location)
            {
                remainingRedirects--;
                Uri redirectUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                currentUri = await ValidatePublicHttpUriAsync(redirectUri.ToString(), cancellationToken).ConfigureAwait(false);
                ValidateAllowedHost(currentUri, allowedHosts);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await OdfBoundedStreamReader.ReadHttpContentAsync(
                response.Content,
                MaxCrlResponseBytes,
                "Err_OdfSignatureTsaClient_ResponseSizeLimitExceeded",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateAllowedHost(Uri uri, ISet<string>? allowedHosts)
    {
        if (allowedHosts is { Count: > 0 } && !allowedHosts.Contains(uri.DnsSafeHost))
            throw new HttpRequestException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_UnsafeCrlUri"));
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect || (int)statusCode == 308;

    internal static async Task<Uri> ValidatePublicHttpUriAsync(
        string value,
        CancellationToken cancellationToken)
    {
        Uri uri = ValidateHttpUri(value);

#if NETSTANDARD2_0
        // netstandard2.0 沒有 SocketsHttpHandler.ConnectCallback，無法把 DNS 驗證結果
        // 固定到實際 socket。預設傳輸只接受公用 IP literal，避免 DNS rebinding 的
        // 檢查／使用時間差；需要 DNS 名稱時由呼叫端注入其受信任 HttpClient。
        if (!IPAddress.TryParse(uri.DnsSafeHost, out IPAddress? literalAddress) ||
            IsPrivateOrSpecialAddress(literalAddress))
        {
            throw new HttpRequestException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_UnsafeCrlUri"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return uri;
#else
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (addresses.Length == 0 || Array.Exists(addresses, IsPrivateOrSpecialAddress))
        {
            throw new HttpRequestException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_UnsafeCrlUri"));
        }

        return uri;
#endif
    }

    private static Uri ValidateHttpUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new HttpRequestException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_UnsafeCrlUri"));
        }

        return uri;
    }

    private static bool IsPrivateOrSpecialAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None) || address.IsIPv6Multicast ||
            address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        byte[] originalBytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
            !address.IsIPv4MappedToIPv6)
        {
            return (originalBytes[0] & 0xfe) == 0xfc;
        }

        byte[] bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 || bytes[0] >= 224 ||
            (bytes[0] == 169 && bytes[1] == 254) ||
            (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) ||
            (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }

    internal static async Task<byte[]> QueryTsaAsync(
        string tsaUrl,
        byte[] hash,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] requestBytes = CreateTsaRequest(hash);

        var client = httpClient ?? s_httpClient;
        using var request = new HttpRequestMessage(HttpMethod.Post, tsaUrl);
        request.Content = new ByteArrayContent(requestBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await OdfBoundedStreamReader.ReadHttpContentAsync(
            response.Content,
            MaxTsaResponseBytes,
            "Err_OdfSignatureTsaClient_ResponseSizeLimitExceeded",
            cancellationToken).ConfigureAwait(false);
    }

    internal static byte[] ExtractTimestampToken(byte[] responseBytes)
    {
        TimeStampResp response;
        try
        {
            response = TimeStampResp.GetInstance(Asn1Object.FromByteArray(responseBytes));
        }
        catch (Exception ex)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_InvalidTsaResponseStructure"), ex);
        }

        int status = response.Status.Status.IntValueExact;
        if (status != 0 && status != 1)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_TsaRequestRejectedStatus", status));

        Org.BouncyCastle.Asn1.Cms.ContentInfo? token = response.TimeStampToken;
        if (token is null)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_TsaResponseContainTimestamptoken"));

        return token.GetEncoded();
    }

    internal static byte[] CanonicalizeSignatureValue(XmlElement signatureValueElem)
    {
        var cleanDoc = new XmlDocument { XmlResolver = null };
        var imported = (XmlElement)cleanDoc.ImportNode(signatureValueElem, true);
        cleanDoc.AppendChild(imported);

        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(imported.SelectNodes("descendant-or-self::node()")!);
        using var tsStream = (Stream)transform.GetOutput(typeof(Stream));
        using var tsMs = new MemoryStream();
        tsStream.CopyTo(tsMs);
        return tsMs.ToArray();
    }

    private static byte[] CreateTsaRequest(byte[] hash)
    {
        if (hash == null || hash.Length != 32)
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSignatureTsaClient_Hash32BytesSha"), nameof(hash));

        byte[] request = new byte[59];
        request[0] = 0x30;
        request[1] = 57;
        request[2] = 0x02;
        request[3] = 0x01;
        request[4] = 0x01;
        request[5] = 0x30;
        request[6] = 49;
        request[7] = 0x30;
        request[8] = 13;
        request[9] = 0x06;
        request[10] = 0x09;
        byte[] sha256Oid = { 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01 };
        Buffer.BlockCopy(sha256Oid, 0, request, 11, 9);
        request[20] = 0x05;
        request[21] = 0x00;
        request[22] = 0x04;
        request[23] = 32;
        Buffer.BlockCopy(hash, 0, request, 24, 32);
        request[56] = 0x01;
        request[57] = 0x01;
        request[58] = 0xff;

        return request;
    }
}
