#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
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
