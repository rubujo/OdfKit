namespace OdfKit.Core;

/// <summary>
/// 定義 ODF 1.3 OpenPGP 加密模式所需的 Session Key 加解密提供者。
/// 實作此介面後，搭配 <see cref="OdfOpenPgpCryptographyProvider"/> 即可完整支援
/// OpenPGP 加密，而無需自行實作 AES-256-CBC 內容層加密邏輯。
/// </summary>
public interface IOdfOpenPgpKeyProvider
{
    /// <summary>
    /// 以指定收件人的 OpenPGP 公鑰加密 Session Key。
    /// </summary>
    /// <param name="sessionKey">明文 Session Key 位元組陣列（32 位元組 AES-256 金鑰）</param>
    /// <param name="recipient">收件人公鑰資訊</param>
    /// <returns>加密後的 Session Key 位元組陣列</returns>
    byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient);

    /// <summary>
    /// 以本地私鑰解密 Session Key。
    /// </summary>
    /// <param name="encryptedKeyPacket">加密後的金鑰封包位元組陣列</param>
    /// <param name="keyId">目標私鑰識別碼，用於選取對應金鑰</param>
    /// <returns>解密後的明文 Session Key 位元組陣列；若無對應私鑰請拋出例外</returns>
    byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId);
}
