using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;

namespace OdfKit.Core;

public sealed partial class OdfBouncyCastleOpenPgpProvider
{
    #region Session Key Payload

    private static byte[] ExtractAndVerifySessionKey(byte[] payload)
    {
        // payload = [1 byte algo][N bytes key][2 bytes checksum]
        if (payload.Length < 4)
            throw new CryptographicException(
                $"解密後的 Session Key Payload 長度不足（{payload.Length} 位元組）。");

        int keyLen = payload.Length - 3;
        byte[] sessionKey = new byte[keyLen];
        Array.Copy(payload, 1, sessionKey, 0, keyLen);

        int expected = 0;
        foreach (byte b in sessionKey)
            expected += b;
        expected &= 0xFFFF;

        int actual = (payload[payload.Length - 2] << 8) | payload[payload.Length - 1];
        if (expected != actual)
            throw new CryptographicException(
                $"Session Key 總和檢查碼驗證失敗（預期 0x{expected:X4}，實際 0x{actual:X4}）。");

        return sessionKey;
    }

    #endregion
}
