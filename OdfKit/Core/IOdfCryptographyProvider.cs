using System;

namespace OdfKit.Core
{
    public interface IOdfCryptographyProvider
    {
        bool CanHandle(OdfEncryptionInfo info);
        byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions);
        byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info);
    }
}
