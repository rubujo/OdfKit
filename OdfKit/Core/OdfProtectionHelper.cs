using System;
using System.Security.Cryptography;
using System.Text;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 節點（工作表、範圍與區段）的密碼保護與雜湊驗證共用協助工具。
/// </summary>
internal static class OdfProtectionHelper
{
    /// <summary>
    /// 使用 PBKDF2-SHA256-50000 演算法對節點啟用保護並寫入密碼雜湊屬性。
    /// </summary>
    /// <param name="node">要保護的 XML 節點。</param>
    /// <param name="password">明文密碼。</param>
    /// <param name="prefix">命名空間前綴。</param>
    /// <param name="nsUri">命名空間 URI。</param>
    public static void ProtectNode(OdfNode node, string password, string prefix, string nsUri)
    {
        if (node is null)
            throw new ArgumentNullException(nameof(node));
        if (password is null)
            throw new ArgumentNullException(nameof(password));

        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] hash = OdfEncryption.Pbkdf2(passwordBytes, salt, 50000, 32, "sha256");

        node.SetAttribute("protected", nsUri, "true", prefix);
        node.SetAttribute("protection-key", nsUri, Convert.ToBase64String(hash), prefix);
        node.SetAttribute("protection-key-digest-algorithm", nsUri, "http://www.w3.org/2001/04/xmlenc#sha256", prefix);
        node.SetAttribute("protection-key-digest-salt", nsUri, Convert.ToBase64String(salt), prefix);
        node.SetAttribute("protection-key-derivation", nsUri, "PBKDF2-SHA256-50000", prefix);
    }

    /// <summary>
    /// 驗證給定密碼是否與節點上的雜湊值吻合。
    /// </summary>
    /// <param name="node">受保護的 XML 節點。</param>
    /// <param name="password">要驗證的密碼。</param>
    /// <param name="nsUri">命名空間 URI。</param>
    /// <returns>若驗證成功或節點未受保護則傳回 <see langword="true"/>；否則傳回 <see langword="false"/>。</returns>
    public static bool VerifyPassword(OdfNode node, string password, string nsUri)
    {
        if (node is null)
            throw new ArgumentNullException(nameof(node));

        string? isProtected = node.GetAttribute("protected", nsUri);
        if (isProtected != "true")
            return true;

        string? keyStr = node.GetAttribute("protection-key", nsUri);
        string? algo = node.GetAttribute("protection-key-digest-algorithm", nsUri);
        string? saltStr = node.GetAttribute("protection-key-digest-salt", nsUri);
        string? derivation = node.GetAttribute("protection-key-derivation", nsUri);

        if (keyStr is null || algo is null || saltStr is null)
            return false;

        byte[] salt = Convert.FromBase64String(saltStr);
        byte[] expectedHash = Convert.FromBase64String(keyStr);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        byte[] input = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

        byte[] actualHash;
        if (derivation == "PBKDF2-SHA256-50000" &&
            (algo == "http://www.w3.org/2001/04/xmlenc#sha256" || algo == "http://www.w3.org/2000/09/xmldsig#sha256"))
        {
            actualHash = OdfEncryption.Pbkdf2(passwordBytes, salt, 50000, 32, "sha256");
        }
        else if (string.IsNullOrEmpty(derivation) &&
                 (algo == "http://www.w3.org/2001/04/xmlenc#sha256" || algo == "http://www.w3.org/2000/09/xmldsig#sha256"))
        {
            using (var sha = SHA256.Create())
            {
                actualHash = sha.ComputeHash(input);
            }
        }
        else
        {
            return false;
        }

        return OdfEncryption.ByteArrayEquals(expectedHash, actualHash);
    }

    /// <summary>
    /// 移除節點上的所有保護屬性，解除其唯讀限制。
    /// </summary>
    /// <param name="node">受保護的 XML 節點。</param>
    /// <param name="nsUri">命名空間 URI。</param>
    public static void UnprotectNode(OdfNode node, string nsUri)
    {
        if (node is null)
            throw new ArgumentNullException(nameof(node));

        node.RemoveAttribute("protected", nsUri);
        node.RemoveAttribute("protection-key", nsUri);
        node.RemoveAttribute("protection-key-digest-algorithm", nsUri);
        node.RemoveAttribute("protection-key-digest-salt", nsUri);
        node.RemoveAttribute("protection-key-derivation", nsUri);
    }
}
