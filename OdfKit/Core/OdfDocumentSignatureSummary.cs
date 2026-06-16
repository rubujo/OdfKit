namespace OdfKit.Core;

/// <summary>
/// 表示文件層數位簽章項目的摘要狀態。
/// </summary>
public sealed class OdfDocumentSignatureSummary
{
    private OdfDocumentSignatureSummary(
        string signatureEntryPath,
        bool hasSignatureEntry,
        bool isSignatureEntryReadable,
        int signatureCount,
        string? errorMessage)
    {
        SignatureEntryPath = signatureEntryPath;
        HasSignatureEntry = hasSignatureEntry;
        IsSignatureEntryReadable = isSignatureEntryReadable;
        SignatureCount = signatureCount;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 取得文件簽章項目在 ODF 封裝中的路徑。
    /// </summary>
    public string SignatureEntryPath { get; }

    /// <summary>
    /// 取得一個值，指出封裝是否包含文件簽章項目。
    /// </summary>
    public bool HasSignatureEntry { get; }

    /// <summary>
    /// 取得一個值，指出文件簽章項目是否可用安全 XML 讀取器解析。
    /// </summary>
    public bool IsSignatureEntryReadable { get; }

    /// <summary>
    /// 取得文件簽章項目內的 XML 數位簽章數量。
    /// </summary>
    public int SignatureCount { get; }

    /// <summary>
    /// 取得一個值，指出文件是否至少包含一個可辨識的 XML 數位簽章。
    /// </summary>
    public bool IsSigned => SignatureCount > 0;

    /// <summary>
    /// 取得讀取文件簽章項目時的錯誤訊息；若可正常讀取則為 <see langword="null"/>。
    /// </summary>
    public string? ErrorMessage { get; }

    internal static OdfDocumentSignatureSummary Unsigned(string signatureEntryPath)
    {
        return new OdfDocumentSignatureSummary(
            signatureEntryPath,
            hasSignatureEntry: false,
            isSignatureEntryReadable: false,
            signatureCount: 0,
            errorMessage: null);
    }

    internal static OdfDocumentSignatureSummary Readable(string signatureEntryPath, int signatureCount)
    {
        return new OdfDocumentSignatureSummary(
            signatureEntryPath,
            hasSignatureEntry: true,
            isSignatureEntryReadable: true,
            signatureCount,
            errorMessage: null);
    }

    internal static OdfDocumentSignatureSummary Unreadable(string signatureEntryPath, string errorMessage)
    {
        return new OdfDocumentSignatureSummary(
            signatureEntryPath,
            hasSignatureEntry: true,
            isSignatureEntryReadable: false,
            signatureCount: 0,
            errorMessage);
    }
}
