using System;
using System.Collections.Generic;

namespace OdfKit.Core;

/// <summary>
/// Describes the encryption metadata attached to an encrypted ODF package entry.
/// 描述加密 ODF 封裝項目附帶的加密中繼資料。
/// </summary>
public sealed class OdfEncryptionInfo
{
    /// <summary>
    /// Gets or sets the checksum algorithm used to verify decrypted entry bytes.
    /// 取得或設定用於驗證解密後專案位元組的總和檢查碼演算法。
    /// </summary>
    public string ChecksumType { get; set; } = "SHA256";

    /// <summary>
    /// Gets or sets the checksum bytes used to verify decrypted entry integrity.
    /// 取得或設定用於驗證解密後專案完整性的總和檢查碼位元組。
    /// </summary>
    public byte[] Checksum { get; set; } = [];

    /// <summary>
    /// Gets or sets the encryption algorithm identifier URI.
    /// 取得或設定加密演算法識別 URI。
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the initialization vector used by the content encryption algorithm.
    /// 取得或設定內容加密演算法使用的初始向量。
    /// </summary>
    public byte[] InitialisationVector { get; set; } = [];

    /// <summary>
    /// Gets or sets the key derivation function name.
    /// 取得或設定金鑰衍生函式名稱。
    /// </summary>
    public string KeyDerivationName { get; set; } = "PBKDF2";

    /// <summary>
    /// Gets or sets the derived content encryption key size in bits.
    /// 取得或設定衍生內容加密金鑰的位元大小。
    /// </summary>
    public int KeySize { get; set; }

    /// <summary>
    /// Gets or sets the iteration count used by the key derivation function.
    /// 取得或設定金鑰衍生函式使用的反覆運算次數。
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Gets or sets the salt bytes used by the key derivation function.
    /// 取得或設定金鑰衍生函式使用的鹽值位元組。
    /// </summary>
    public byte[] Salt { get; set; } = [];

    /// <summary>
    /// Gets or sets the start-key generation algorithm name.
    /// 取得或設定起始金鑰產生演算法名稱。
    /// </summary>
    public string? StartKeyGenerationName { get; set; }

    /// <summary>
    /// Gets or sets the start-key size in bits.
    /// 取得或設定起始金鑰位元大小。
    /// </summary>
    public int? StartKeySize { get; set; }

    /// <summary>
    /// Gets or sets vendor-specific encryption metadata properties.
    /// 取得或設定供特定供應商使用的加密中繼資料屬性。
    /// </summary>
    public Dictionary<string, string> ExtensionProperties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the OpenPGP encrypted key descriptors declared in <c>manifest:encrypted-key</c>.
    /// 取得 <c>manifest:encrypted-key</c> 宣告的 OpenPGP 加密金鑰描述集合。
    /// </summary>
    public List<OdfOpenPgpEncryptedKeyInfo> OpenPgpEncryptedKeys { get; } = [];

    internal bool HasChecksumType { get; set; }
    internal bool HasChecksum { get; set; }
    internal bool HasAlgorithmName { get; set; }
    internal bool HasInitialisationVector { get; set; }
    internal bool HasKeyDerivationName { get; set; }
    internal bool HasIterationCount { get; set; }
    internal bool HasSalt { get; set; }
}

/// <summary>
/// Describes an OpenPGP recipient used when encrypting an ODF package.
/// 描述加密 ODF 封裝時使用的 OpenPGP 收件者。
/// </summary>
public sealed class OdfOpenPgpRecipient
{
    /// <summary>
    /// Gets or sets the OpenPGP key identifier.
    /// 取得或設定 OpenPGP 金鑰識別碼。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient display name or email address.
    /// 取得或設定收件者顯示名稱或電子郵件。
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the public key material consumed by the custom provider.
    /// 取得或設定供自訂提供者使用的公開金鑰資料。
    /// </summary>
    public byte[] PublicKey { get; set; } = [];
}

/// <summary>
/// Describes an OpenPGP encrypted key entry read from <c>manifest:encrypted-key</c>.
/// 描述從 <c>manifest:encrypted-key</c> 讀取的 OpenPGP 加密金鑰項目。
/// </summary>
public sealed class OdfOpenPgpEncryptedKeyInfo
{
    /// <summary>
    /// Gets or sets the OpenPGP key identifier.
    /// 取得或設定 OpenPGP 金鑰識別碼。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient display name or email address.
    /// 取得或設定收件者顯示名稱或電子郵件。
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the algorithm identifier used to encrypt the session key packet.
    /// 取得或設定用於加密工作階段金鑰封包的演算法識別碼。
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the encrypted session key packet.
    /// 取得或設定加密後的工作階段金鑰封包。
    /// </summary>
    public byte[] KeyPacket { get; set; } = [];

    /// <summary>
    /// Gets vendor-specific encrypted-key metadata properties.
    /// 取得供特定供應商使用的 encrypted-key 中繼資料屬性。
    /// </summary>
    public Dictionary<string, string> ExtensionProperties { get; } = new(StringComparer.Ordinal);
}

