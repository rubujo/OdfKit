using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Defines how a LibreOffice macro-signing certificate is trusted.
/// 定義 LibreOffice 巨集簽署憑證的信任方式。
/// </summary>
public enum OdfMacroTrustMode
{
    /// <summary>
    /// Uses the operating system certificate stores.
    /// 使用作業系統憑證存放區。
    /// </summary>
    System,

    /// <summary>
    /// Requires the chain to terminate at an explicitly supplied root.
    /// 要求憑證鏈終止於明確提供的根憑證。
    /// </summary>
    CustomRoot,

    /// <summary>
    /// Requires the signing certificate SHA-256 fingerprint to be pinned.
    /// 要求簽署憑證的 SHA-256 指紋符合釘選值。
    /// </summary>
    PinnedCertificate
}

/// <summary>
/// Reports the trust decision for LibreOffice macro signatures.
/// 回報 LibreOffice 巨集簽章的信任判定。
/// </summary>
public enum OdfMacroTrustStatus
{
    /// <summary>
    /// No macro signature was present.
    /// 不存在巨集簽章。
    /// </summary>
    Unsigned,

    /// <summary>
    /// At least one cryptographic signature was invalid.
    /// 至少有一個密碼學簽章無效。
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// Signatures were valid but the selected trust policy rejected a signer.
    /// 簽章有效，但選定的信任政策拒絕了簽署者。
    /// </summary>
    Untrusted,

    /// <summary>
    /// All signatures and signer trust decisions succeeded.
    /// 所有簽章與簽署者信任判定皆成功。
    /// </summary>
    Trusted
}

/// <summary>
/// Configures certificate trust for LibreOffice macro signature validation.
/// 設定 LibreOffice 巨集簽章驗證的憑證信任政策。
/// </summary>
public sealed class OdfMacroTrustPolicy
{
    /// <summary>
    /// Gets or sets the trust mode.
    /// 取得或設定信任模式。
    /// </summary>
    public OdfMacroTrustMode Mode { get; set; } = OdfMacroTrustMode.System;

    /// <summary>
    /// Gets or sets whether certificate revocation is checked.
    /// 取得或設定是否檢查憑證撤銷狀態。
    /// </summary>
    public bool CheckRevocation { get; set; }

    /// <summary>
    /// Gets custom trust anchors used by <see cref="OdfMacroTrustMode.CustomRoot"/>.
    /// 取得 <see cref="OdfMacroTrustMode.CustomRoot"/> 使用的自訂信任錨點。
    /// </summary>
    public X509Certificate2Collection CustomRoots { get; } = new();

    /// <summary>
    /// Gets intermediate certificates supplied while building custom chains.
    /// 取得建立自訂憑證鏈時提供的中繼憑證。
    /// </summary>
    public X509Certificate2Collection IntermediateCertificates { get; } = new();

    /// <summary>
    /// Gets normalized SHA-256 signing-certificate fingerprints used by pinning.
    /// 取得憑證釘選所使用的正規化 SHA-256 簽署憑證指紋。
    /// </summary>
    public ISet<string> PinnedCertificateSha256 { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Combines cryptographic validation with an explicit macro trust decision.
/// 結合密碼學驗證結果與明確的巨集信任判定。
/// </summary>
public sealed class OdfMacroSignatureValidationResult
{
    internal OdfMacroSignatureValidationResult(
        OdfSignatureValidationResult cryptographicValidation,
        OdfMacroTrustStatus trustStatus)
    {
        CryptographicValidation = cryptographicValidation;
        TrustStatus = trustStatus;
    }

    /// <summary>
    /// Gets the underlying XMLDSig and XAdES validation result.
    /// 取得底層 XMLDSig 與 XAdES 驗證結果。
    /// </summary>
    public OdfSignatureValidationResult CryptographicValidation { get; }

    /// <summary>
    /// Gets the trust-policy decision.
    /// 取得信任政策判定。
    /// </summary>
    public OdfMacroTrustStatus TrustStatus { get; }

    /// <summary>
    /// Gets whether every macro signature is valid and trusted.
    /// 取得每個巨集簽章是否皆有效且受信任。
    /// </summary>
    public bool IsTrusted => TrustStatus == OdfMacroTrustStatus.Trusted;

    /// <summary>
    /// Gets whether macro code safety was evaluated; certificate trust does not establish code safety.
    /// 取得是否已評估巨集程式碼安全性；憑證信任不代表程式碼安全。
    /// </summary>
    public bool IsCodeSafetyEvaluated => false;
}

public sealed partial class OdfScriptManager
{
    private static readonly OdfSignatureProfile MacroSignatureProfile = new(
        MacroSignaturePath,
        IsMacroSignatureEntry);

    /// <summary>
    /// Signs recognized LibreOffice macro package entries with default XMLDSig options.
    /// 使用預設 XMLDSig 選項簽署已辨識的 LibreOffice 巨集封裝項目。
    /// </summary>
    /// <param name="certificate">The signing certificate with a private key. / 含私密金鑰的簽署憑證。</param>
    /// <returns>A task representing the signing operation. / 代表簽署作業的工作。</returns>
    public Task SignLibreOfficeMacrosAsync(X509Certificate2 certificate) =>
        SignLibreOfficeMacrosAsync(certificate, new OdfSigningOptions(), default);

    /// <summary>
    /// Signs recognized LibreOffice macro package entries with default XMLDSig options.
    /// 使用預設 XMLDSig 選項簽署已辨識的 LibreOffice 巨集封裝項目。
    /// </summary>
    /// <param name="certificate">The signing certificate with a private key. / 含私密金鑰的簽署憑證。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the signing operation. / 代表簽署作業的工作。</returns>
    public Task SignLibreOfficeMacrosAsync(X509Certificate2 certificate, CancellationToken cancellationToken) =>
        SignLibreOfficeMacrosAsync(certificate, new OdfSigningOptions(), cancellationToken);

    /// <summary>
    /// Signs recognized LibreOffice macro package entries.
    /// 簽署已辨識的 LibreOffice 巨集封裝項目。
    /// </summary>
    /// <param name="certificate">The signing certificate with a private key. / 含私密金鑰的簽署憑證。</param>
    /// <param name="options">The XMLDSig and XAdES options. / XMLDSig 與 XAdES 選項。</param>
    /// <returns>A task representing the signing operation. / 代表簽署作業的工作。</returns>
    public Task SignLibreOfficeMacrosAsync(X509Certificate2 certificate, OdfSigningOptions options) =>
        SignLibreOfficeMacrosAsync(certificate, options, default);

    /// <summary>
    /// Signs recognized LibreOffice macro package entries.
    /// 簽署已辨識的 LibreOffice 巨集封裝項目。
    /// </summary>
    /// <param name="certificate">The signing certificate with a private key. / 含私密金鑰的簽署憑證。</param>
    /// <param name="options">The XMLDSig and XAdES options. / XMLDSig 與 XAdES 選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the signing operation. / 代表簽署作業的工作。</returns>
    public Task SignLibreOfficeMacrosAsync(
        X509Certificate2 certificate,
        OdfSigningOptions options,
        CancellationToken cancellationToken)
    {
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate), OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(certificate)));
        if (options is null)
            throw new ArgumentNullException(nameof(options), OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(options)));
        EnsurePackageScriptsSupported();
        if (!GetPackageScripts().Any())
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfScriptManager_UnsupportedOperation"));
        return OdfSignatureSigner.SignAsync(_package, certificate, options, MacroSignatureProfile, cancellationToken);
    }

    /// <summary>
    /// Removes LibreOffice macro signature files without changing script content.
    /// 移除 LibreOffice 巨集簽章檔案，但不變更指令碼內容。
    /// </summary>
    /// <returns><see langword="true"/> if a current or legacy signature file was removed; otherwise, <see langword="false"/>. / 若已移除目前或舊式簽章檔案則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveLibreOfficeMacroSignatures()
    {
        EnsurePackageScriptsSupported();
        bool removed = _package.RemoveEntry(MacroSignaturePath);
        return _package.RemoveEntry(LegacyMacroSignaturePath) || removed;
    }

    /// <summary>
    /// Validates LibreOffice macro signatures against operating-system trust.
    /// 依作業系統信任設定驗證 LibreOffice 巨集簽章。
    /// </summary>
    /// <returns>The macro signature validation result. / 巨集簽章驗證結果。</returns>
    public Task<OdfMacroSignatureValidationResult> VerifyLibreOfficeMacroSignaturesAsync() =>
        VerifyLibreOfficeMacroSignaturesAsync(new OdfMacroTrustPolicy(), default);

    /// <summary>
    /// Validates LibreOffice macro signatures against operating-system trust.
    /// 依作業系統信任設定驗證 LibreOffice 巨集簽章。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The macro signature validation result. / 巨集簽章驗證結果。</returns>
    public Task<OdfMacroSignatureValidationResult> VerifyLibreOfficeMacroSignaturesAsync(CancellationToken cancellationToken) =>
        VerifyLibreOfficeMacroSignaturesAsync(new OdfMacroTrustPolicy(), cancellationToken);

    /// <summary>
    /// Validates LibreOffice macro signatures against an explicit trust policy.
    /// 依明確的信任政策驗證 LibreOffice 巨集簽章。
    /// </summary>
    /// <param name="policy">The certificate trust policy. / 憑證信任政策。</param>
    /// <returns>The macro signature validation result. / 巨集簽章驗證結果。</returns>
    public Task<OdfMacroSignatureValidationResult> VerifyLibreOfficeMacroSignaturesAsync(OdfMacroTrustPolicy policy) =>
        VerifyLibreOfficeMacroSignaturesAsync(policy, default);

    /// <summary>
    /// Validates LibreOffice macro signatures against an explicit trust policy.
    /// 依明確的信任政策驗證 LibreOffice 巨集簽章。
    /// </summary>
    /// <param name="policy">The certificate trust policy. / 憑證信任政策。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The macro signature validation result. / 巨集簽章驗證結果。</returns>
    public async Task<OdfMacroSignatureValidationResult> VerifyLibreOfficeMacroSignaturesAsync(
        OdfMacroTrustPolicy policy,
        CancellationToken cancellationToken)
    {
        if (policy is null)
            throw new ArgumentNullException(nameof(policy), OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(policy)));
        if (!Enum.IsDefined(typeof(OdfMacroTrustMode), policy.Mode))
            throw new ArgumentOutOfRangeException(nameof(policy), OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(policy)));
        EnsurePackageScriptsSupported();

        var options = new OdfSigningOptions
        {
            AllowUntrustedRoot = policy.Mode != OdfMacroTrustMode.System,
            CheckRevocation = policy.CheckRevocation
        };
        options.ExtraCertificates.AddRange(policy.IntermediateCertificates);
        options.ExtraCertificates.AddRange(policy.CustomRoots);
        OdfSignatureValidationResult validation = await OdfSignatureVerifier.VerifySignaturesAsync(
            _package,
            options,
            MacroSignatureProfile,
            cancellationToken).ConfigureAwait(false);

        return new OdfMacroSignatureValidationResult(validation, EvaluateTrust(validation, policy));
    }

    private static OdfMacroTrustStatus EvaluateTrust(
        OdfSignatureValidationResult validation,
        OdfMacroTrustPolicy policy)
    {
        if (validation.Signatures.Count == 0)
            return OdfMacroTrustStatus.Unsigned;
        if (!validation.IsValid)
            return OdfMacroTrustStatus.InvalidSignature;
        if (policy.Mode == OdfMacroTrustMode.System)
            return OdfMacroTrustStatus.Trusted;

        foreach (OdfSingleSignatureValidationResult signature in validation.Signatures)
        {
            if (signature.Certificate is null || !IsSignerTrusted(signature.Certificate, policy))
                return OdfMacroTrustStatus.Untrusted;
        }

        return OdfMacroTrustStatus.Trusted;
    }

    private static bool IsSignerTrusted(X509Certificate2 certificate, OdfMacroTrustPolicy policy)
    {
        if (policy.Mode == OdfMacroTrustMode.PinnedCertificate)
        {
            using SHA256 sha256 = SHA256.Create();
            string fingerprint = BitConverter.ToString(sha256.ComputeHash(certificate.RawData)).Replace("-", string.Empty);
            return policy.PinnedCertificateSha256.Any(pin => NormalizeFingerprint(pin) == fingerprint);
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = policy.CheckRevocation
            ? X509RevocationMode.Online
            : X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.AddRange(policy.IntermediateCertificates);
        chain.ChainPolicy.ExtraStore.AddRange(policy.CustomRoots);
        _ = chain.Build(certificate);
        if (chain.ChainElements.Count == 0)
            return false;

        X509Certificate2 terminal = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        return policy.CustomRoots.Cast<X509Certificate2>()
            .Any(root => root.RawData.SequenceEqual(terminal.RawData));
    }

    private static string? NormalizeFingerprint(string? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value))
            return null;
        string normalized = value.Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit) ? normalized : null;
    }

    private static bool IsMacroSignatureEntry(string path)
    {
        if (string.IsNullOrEmpty(path) || path.EndsWith("/", StringComparison.Ordinal))
            return false;
        return path.StartsWith(BasicRoot, StringComparison.Ordinal)
            || path.StartsWith(PythonRoot, StringComparison.Ordinal);
    }
}
