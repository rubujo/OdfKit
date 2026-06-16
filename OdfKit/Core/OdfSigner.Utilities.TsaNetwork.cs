using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using OdfKit.Core;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region TSA & Network Utilities

    private static bool StructuralEqual(byte[] a, byte[] b)
    {
        return OdfEncryption.ByteArrayEquals(a, b);
    }

    internal static async Task<byte[]> DownloadCrlAsync(string url, HttpClient? httpClient)
    {
        var client = httpClient ?? s_httpClient;
        return await client.GetByteArrayAsync(url);
    }

    private static byte[] CreateTsaRequest(byte[] hash)
    {
        if (hash == null || hash.Length != 32)
            throw new ArgumentException("Hash must be 32 bytes (SHA-256).", nameof(hash));

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

    internal static async Task<byte[]> QueryTsaAsync(string tsaUrl, byte[] hash, HttpClient? httpClient)
    {
        byte[] requestBytes = CreateTsaRequest(hash);

        var client = httpClient ?? s_httpClient;
        using var request = new HttpRequestMessage(HttpMethod.Post, tsaUrl);
        request.Content = new ByteArrayContent(requestBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    internal static byte[] ExtractTimestampToken(byte[] responseBytes)
    {
        var root = ParseDer(responseBytes);
        if (root.Tag != 0x30 || root.Children.Count < 1)
        {
            throw new CryptographicException("Invalid TSA response structure (expected SEQUENCE).");
        }

        var statusInfo = root.Children[0];
        if (statusInfo.Tag != 0x30 || statusInfo.Children.Count < 1)
        {
            throw new CryptographicException("Invalid PKIStatusInfo structure.");
        }

        int status = ParseInteger(statusInfo.Children[0].Value);
        if (status != 0 && status != 1)
        {
            throw new CryptographicException($"TSA request was rejected with status: {status}.");
        }

        if (root.Children.Count < 2)
        {
            throw new CryptographicException("TSA response does not contain a TimeStampToken.");
        }

        var token = root.Children[1];
        return token.RawBytes;
    }

    #endregion
}
