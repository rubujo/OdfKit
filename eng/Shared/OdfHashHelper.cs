using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace OdfKit.Internal;

/// <summary>
/// Provides cross-target one-shot hashing operations.
/// 提供跨目標的一次性雜湊運算。
/// </summary>
internal static class OdfHashHelper
{
    /// <summary>
    /// Computes a SHA-256 digest.
    /// 計算 SHA-256 摘要。
    /// </summary>
    public static byte[] Sha256(byte[] value)
    {
#if NET6_0_OR_GREATER
        return SHA256.HashData(value);
#else
        using (var algorithm = SHA256.Create())
        {
            return algorithm.ComputeHash(value);
        }
#endif
    }

    /// <summary>
    /// Computes a SHA-1 digest required by legacy ODF profiles.
    /// 計算舊版 ODF 設定檔所需的 SHA-1 摘要。
    /// </summary>
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "SHA-1 is required only to read and write legacy ODF encryption and XML signature profiles; modern profiles use SHA-256.")]
    public static byte[] Sha1(byte[] value)
    {
#if NET6_0_OR_GREATER
        return SHA1.HashData(value);
#else
#pragma warning disable SYSLIB0021
        using (var algorithm = SHA1.Create())
        {
            return algorithm.ComputeHash(value);
        }
#pragma warning restore SYSLIB0021
#endif
    }

    /// <summary>Computes an uppercase SHA-256 hexadecimal digest. / 計算大寫 SHA-256 十六進位摘要。</summary>
    public static string Sha256Hex(byte[] value)
    {
        byte[] hash = Sha256(value);
#if NET6_0_OR_GREATER
        return Convert.ToHexString(hash);
#else
        return BitConverter.ToString(hash).Replace("-", string.Empty);
#endif
    }
}
