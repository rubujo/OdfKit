using System;
using System.IO;
using System.Net.Http;
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
    private static readonly HttpClient s_httpClient = new();

    internal static async Task<byte[]> DownloadCrlAsync(
        string url,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = httpClient ?? s_httpClient;
        using HttpResponseMessage response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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
        var cleanDoc = new XmlDocument();
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
