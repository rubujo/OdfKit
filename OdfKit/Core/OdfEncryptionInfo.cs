using System;
using System.Collections.Generic;

namespace OdfKit.Core;

/// <summary>
/// 包含 ODF 封裝專案的加密與解密詳細資訊。
/// </summary>
public sealed class OdfEncryptionInfo
{
    /// <summary>
    /// 取得或設定總和檢查碼類型，預設為 SHA256 。
    /// </summary>
    public string ChecksumType { get; set; } = "SHA256";

    /// <summary>
    /// 取得或設定用來驗證解密後檔案完整性的總和檢查碼。
    /// </summary>
    public byte[] Checksum { get; set; } = [];

    /// <summary>
    /// 取得或設定加密演算法名稱（識別 URI ）。
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定初始向量。
    /// </summary>
    public byte[] InitialisationVector { get; set; } = [];

    /// <summary>
    /// 取得或設定金鑰衍生函式名稱，預設為 PBKDF2 。
    /// </summary>
    public string KeyDerivationName { get; set; } = "PBKDF2";

    /// <summary>
    /// 取得或設定金鑰大小。
    /// </summary>
    public int KeySize { get; set; }

    /// <summary>
    /// 取得或設定金鑰衍生的反覆運算次數。
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// 取得或設定金鑰衍生的鹽值。
    /// </summary>
    public byte[] Salt { get; set; } = [];

    /// <summary>
    /// 取得或設定起始金鑰產生的演算法名稱。
    /// </summary>
    public string? StartKeyGenerationName { get; set; }

    /// <summary>
    /// 取得或設定起始金鑰大小。
    /// </summary>
    public int? StartKeySize { get; set; }

    /// <summary>
    /// 取得其他擴充的加密屬性。
    /// </summary>
    public Dictionary<string, string> ExtensionProperties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 取得 OpenPGP 加密金鑰描述集合，對應 manifest:encrypted-key。
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
/// 表示 OpenPGP 加密收件者。
/// </summary>
public sealed class OdfOpenPgpRecipient
{
    /// <summary>
    /// 取得或設定 OpenPGP 金鑰識別碼。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定收件者顯示名稱或電子郵件。
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// 取得或設定供自訂提供者使用的公開金鑰資料。
    /// </summary>
    public byte[] PublicKey { get; set; } = [];
}

/// <summary>
/// 表示 manifest:encrypted-key 中的 OpenPGP 加密金鑰資訊。
/// </summary>
public sealed class OdfOpenPgpEncryptedKeyInfo
{
    /// <summary>
    /// 取得或設定 OpenPGP 金鑰識別碼。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定收件者顯示名稱或電子郵件。
    /// </summary>
    public string? Recipient { get; set; }

    /// <summary>
    /// 取得或設定加密金鑰演算法名稱。
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定加密後的工作階段金鑰封包。
    /// </summary>
    public byte[] KeyPacket { get; set; } = [];

    /// <summary>
    /// 取得其他 encrypted-key 擴充屬性。
    /// </summary>
    public Dictionary<string, string> ExtensionProperties { get; } = new(StringComparer.Ordinal);
}

