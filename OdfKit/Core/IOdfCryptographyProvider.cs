#pragma warning restore CS1591

using System;

namespace OdfKit.Core;

/// <summary>
/// 定義 ODF 文件加密與解密操作的密碼學提供者介面。
/// </summary>
public interface IOdfCryptographyProvider
{
    /// <summary>
    /// 確定此提供者是否能夠處理指定的加密資訊。
    /// </summary>
    /// <param name="info">加密資訊的詳細資料</param>
    /// <returns>若可以處理，則為 <see langword="true"/> ；否則為 <see langword="false"/></returns>
    bool CanHandle(OdfEncryptionInfo info);

    /// <summary>
    /// 使用指定的加密資訊與載入選項解密檔案內容。
    /// </summary>
    /// <param name="ciphertext">要解密的密文位元組陣列</param>
    /// <param name="info">用於解密的加密資訊</param>
    /// <param name="loadOptions">載入文件的選項，包含金鑰或密碼</param>
    /// <returns>解密後的明文位元組陣列</returns>
    byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions);

    /// <summary>
    /// 加密指定的明文內容，並產生對應的加密資訊。
    /// </summary>
    /// <param name="plaintext">要加密的明文位元組陣列</param>
    /// <param name="entryPath">該封裝專案的路徑</param>
    /// <param name="saveOptions">儲存文件的選項，包含金鑰或密碼</param>
    /// <param name="info">輸出產生的加密資訊</param>
    /// <returns>加密後的密文位元組陣列</returns>
    byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info);
}

