using System;

namespace OdfKit.Core;

/// <summary>
/// 定義簽章描述檔路徑及必須涵蓋的封裝項目。
/// </summary>
internal sealed class OdfSignatureProfile(
    string signaturePath,
    Func<string, bool> isCoverableEntry)
{
    internal static OdfSignatureProfile Document { get; } = new(
        OdfSignerConstants.SignaturePath,
        OdfSignerConstants.IsCoverableEntry);

    internal string SignaturePath { get; } = signaturePath;

    internal Func<string, bool> IsCoverableEntry { get; } = isCoverableEntry;
}
