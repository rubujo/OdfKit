using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core;

/// <summary>
/// 表示 ODF 封裝中所有簽章的驗證結果。
/// </summary>
public sealed class OdfSignatureValidationResult
{
    /// <summary>
    /// 取得一個值，指出封裝中的所有簽章是否皆為有效。
    /// </summary>
    public bool IsValid { get; internal set; }

    /// <summary>
    /// 取得個別簽章驗證結果的集合。
    /// </summary>
    public List<OdfSingleSignatureValidationResult> Signatures { get; } = [];
}

/// <summary>
/// 表示單一數位簽章的詳細驗證結果。
/// </summary>
public sealed class OdfSingleSignatureValidationResult
{
    /// <summary>
    /// 取得簽章識別碼（若可用）。
    /// </summary>
    public string? SignatureId { get; internal set; }

    /// <summary>
    /// 取得 XML 密碼學簽章是否有效。
    /// </summary>
    public bool IsSignatureValid { get; internal set; }

    /// <summary>
    /// 取得簽署憑證。
    /// </summary>
    public X509Certificate2? Certificate { get; internal set; }

    /// <summary>
    /// 取得簽署憑證是否有效（例如未過期且已生效）。
    /// </summary>
    public bool IsCertificateValid { get; internal set; }

    /// <summary>
    /// 取得憑證鏈是否有效。
    /// </summary>
    public bool IsChainValid { get; internal set; }

    /// <summary>
    /// 取得 XAdES-T 時間戳記（若存在）在密碼學上是否有效。
    /// </summary>
    public bool IsTimestampValid { get; internal set; }

    /// <summary>
    /// 取得撤銷檢查（ CRL/OCSP ）是否成功且憑證未被撤銷。
    /// </summary>
    public bool IsRevocationValid { get; internal set; }

    /// <summary>
    /// 取得驗證過程中遇到的任何錯誤或警告訊息。
    /// </summary>
    public string? ErrorMessage { get; internal set; }

    /// <summary>
    /// 取得驗證失敗時的診斷錯誤碼。
    /// </summary>
    public string? ErrorCode { get; internal set; }

    /// <summary>
    /// 取得驗證過程中遇到的警告清單。
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// 取得此簽章所驗證的 ODF 封裝參考項目清單。
    /// </summary>
    public List<string> CheckedReferences { get; } = [];

    /// <summary>
    /// 取得已執行驗證步驟的追蹤記錄。
    /// </summary>
    public List<string> ValidationSteps { get; } = [];
}

