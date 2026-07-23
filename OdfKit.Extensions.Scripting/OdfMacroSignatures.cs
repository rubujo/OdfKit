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
/// Defines certificate revocation behavior for macro signer trust.
/// 定義巨集簽署者信任的憑證撤銷行為。
/// </summary>
public enum OdfMacroRevocationMode
{
    /// <summary>
    /// Does not check certificate revocation.
    /// 不檢查憑證撤銷狀態。
    /// </summary>
    NoCheck,

    /// <summary>
    /// Performs an online revocation check.
    /// 執行線上撤銷檢查。
    /// </summary>
    Online,

    /// <summary>
    /// Uses only revocation data already cached by the operating system.
    /// 僅使用作業系統已快取的撤銷資料。
    /// </summary>
    OfflineCache
}

/// <summary>
/// Identifies reasons a cryptographically valid macro signer was rejected.
/// 識別密碼學有效的巨集簽署者遭拒原因。
/// </summary>
[Flags]
public enum OdfMacroTrustFailure
{
    /// <summary>
    /// No trust-policy failure was identified.
    /// 未識別到信任政策失敗。
    /// </summary>
    None = 0,

    /// <summary>
    /// The certificate chain was rejected.
    /// 憑證鏈遭拒。
    /// </summary>
    CertificateChain = 1,

    /// <summary>
    /// The certificate pin was absent or outside its rotation window.
    /// 憑證釘選不存在或不在輪替時窗內。
    /// </summary>
    CertificatePin = 2,

    /// <summary>
    /// The signer subject was not allowed.
    /// 簽署者主體不在允許清單內。
    /// </summary>
    Subject = 4,

    /// <summary>
    /// The signer issuer was not allowed.
    /// 簽署者簽發者不在允許清單內。
    /// </summary>
    Issuer = 8,

    /// <summary>
    /// The signer did not contain an allowed enhanced key usage.
    /// 簽署者未包含允許的增強金鑰用途。
    /// </summary>
    EnhancedKeyUsage = 16,

    /// <summary>
    /// Revocation data was missing, stale, or indicated revocation.
    /// 撤銷資料遺失、過期或指出憑證已撤銷。
    /// </summary>
    Revocation = 32
}

/// <summary>
/// Defines a certificate pin with an optional activation window for signer rotation.
/// 定義含選用啟用時窗的憑證釘選，以支援簽署者輪替。
/// </summary>
public sealed class OdfMacroSignerPin
{
    /// <summary>
    /// Initializes a rotating signer pin.
    /// 初始化輪替簽署者釘選。
    /// </summary>
    /// <param name="sha256Fingerprint">The SHA-256 certificate fingerprint. / 憑證 SHA-256 指紋。</param>
    public OdfMacroSignerPin(string sha256Fingerprint)
    {
        Sha256Fingerprint = sha256Fingerprint ?? throw new ArgumentNullException(
            nameof(sha256Fingerprint),
            OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(sha256Fingerprint)));
    }

    /// <summary>
    /// Gets the SHA-256 certificate fingerprint.
    /// 取得憑證 SHA-256 指紋。
    /// </summary>
    public string Sha256Fingerprint { get; }

    /// <summary>
    /// Gets or sets the inclusive activation time.
    /// 取得或設定包含端點的啟用時間。
    /// </summary>
    public DateTimeOffset? ActiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the exclusive retirement time.
    /// 取得或設定不含端點的停用時間。
    /// </summary>
    public DateTimeOffset? ActiveUntil { get; set; }
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
    /// Gets or sets explicit revocation behavior; <see cref="CheckRevocation"/> retains precedence for compatibility.
    /// 取得或設定明確的撤銷行為；為維持相容性，<see cref="CheckRevocation"/> 具有較高優先權。
    /// </summary>
    public OdfMacroRevocationMode RevocationMode { get; set; }

    /// <summary>
    /// Gets or sets the trust evaluation time used by certificate rotation windows.
    /// 取得或設定憑證輪替時窗所使用的信任評估時間。
    /// </summary>
    public DateTimeOffset? VerificationTime { get; set; }

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

    /// <summary>
    /// Gets time-bounded certificate pins used during signer rotation.
    /// 取得簽署者輪替期間使用且具時間界線的憑證釘選。
    /// </summary>
    public IList<OdfMacroSignerPin> RotatingCertificatePins { get; } = new List<OdfMacroSignerPin>();

    /// <summary>
    /// Gets exact, case-insensitive distinguished names allowed for signer subjects.
    /// 取得簽署者主體允許的完整辨別名稱，且比對不分大小寫。
    /// </summary>
    public ISet<string> AllowedSubjects { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets exact, case-insensitive distinguished names allowed for certificate issuers.
    /// 取得憑證簽發者允許的完整辨別名稱，且比對不分大小寫。
    /// </summary>
    public ISet<string> AllowedIssuers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets enhanced-key-usage OIDs of which at least one must appear on a signer.
    /// 取得簽署者至少須包含一項的增強金鑰用途 OID。
    /// </summary>
    public ISet<string> AllowedEnhancedKeyUsages { get; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>
/// Combines cryptographic validation with an explicit macro trust decision.
/// 結合密碼學驗證結果與明確的巨集信任判定。
/// </summary>
public sealed class OdfMacroSignatureValidationResult
{
    internal OdfMacroSignatureValidationResult(
        OdfSignatureValidationResult cryptographicValidation,
        OdfMacroTrustStatus trustStatus,
        OdfMacroTrustFailure trustFailures)
    {
        CryptographicValidation = cryptographicValidation;
        TrustStatus = trustStatus;
        TrustFailures = trustFailures;
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
    /// Gets the accumulated signer-policy rejection reasons.
    /// 取得累積的簽署者政策拒絕原因。
    /// </summary>
    public OdfMacroTrustFailure TrustFailures { get; }

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

/// <summary>
/// Provides macro-signing and trust-verification operations.
/// 提供巨集簽署與信任驗證作業。
/// </summary>
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
        if (!Enum.IsDefined(typeof(OdfMacroRevocationMode), policy.RevocationMode))
            throw new ArgumentOutOfRangeException(nameof(policy), OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(policy)));
        EnsurePackageScriptsSupported();

        OdfMacroRevocationMode revocationMode = GetRevocationMode(policy);

        var options = new OdfSigningOptions
        {
            AllowUntrustedRoot = policy.Mode != OdfMacroTrustMode.System,
            CheckRevocation = revocationMode == OdfMacroRevocationMode.Online
        };
        options.ExtraCertificates.AddRange(policy.IntermediateCertificates);
        options.ExtraCertificates.AddRange(policy.CustomRoots);
        OdfSignatureValidationResult validation = await OdfSignatureVerifier.VerifySignaturesAsync(
            _package,
            options,
            MacroSignatureProfile,
            cancellationToken).ConfigureAwait(false);

        (OdfMacroTrustStatus status, OdfMacroTrustFailure failures) = EvaluateTrust(validation, policy);
        return new OdfMacroSignatureValidationResult(validation, status, failures);
    }

    private static (OdfMacroTrustStatus Status, OdfMacroTrustFailure Failures) EvaluateTrust(
        OdfSignatureValidationResult validation,
        OdfMacroTrustPolicy policy)
    {
        if (validation.Signatures.Count == 0)
            return (OdfMacroTrustStatus.Unsigned, OdfMacroTrustFailure.None);
        if (!validation.IsValid)
            return (OdfMacroTrustStatus.InvalidSignature, OdfMacroTrustFailure.None);

        OdfMacroTrustFailure failures = OdfMacroTrustFailure.None;
        foreach (OdfSingleSignatureValidationResult signature in validation.Signatures)
        {
            failures |= signature.Certificate is null
                ? OdfMacroTrustFailure.CertificateChain
                : EvaluateSignerTrust(signature.Certificate, policy);
        }

        return failures == OdfMacroTrustFailure.None
            ? (OdfMacroTrustStatus.Trusted, failures)
            : (OdfMacroTrustStatus.Untrusted, failures);
    }

    private static OdfMacroTrustFailure EvaluateSignerTrust(
        X509Certificate2 certificate,
        OdfMacroTrustPolicy policy)
    {
        OdfMacroTrustFailure failures = EvaluateSignerIdentity(certificate, policy);
        if (policy.Mode == OdfMacroTrustMode.PinnedCertificate)
        {
            using SHA256 sha256 = SHA256.Create();
            string fingerprint = BitConverter.ToString(sha256.ComputeHash(certificate.RawData)).Replace("-", string.Empty);
            DateTimeOffset verificationTime = policy.VerificationTime ?? DateTimeOffset.UtcNow;
            bool isPinned = policy.PinnedCertificateSha256.Any(pin => NormalizeFingerprint(pin) == fingerprint) ||
                policy.RotatingCertificatePins.Any(pin =>
                    NormalizeFingerprint(pin.Sha256Fingerprint) == fingerprint &&
                    (!pin.ActiveFrom.HasValue || verificationTime >= pin.ActiveFrom.Value) &&
                    (!pin.ActiveUntil.HasValue || verificationTime < pin.ActiveUntil.Value));
            if (!isPinned)
                failures |= OdfMacroTrustFailure.CertificatePin;
            if (GetRevocationMode(policy) != OdfMacroRevocationMode.NoCheck)
                failures |= EvaluateRevocation(certificate, policy);
            return failures;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = ToX509RevocationMode(GetRevocationMode(policy));
        chain.ChainPolicy.VerificationTime = (policy.VerificationTime ?? DateTimeOffset.UtcNow).LocalDateTime;
        chain.ChainPolicy.ExtraStore.AddRange(policy.IntermediateCertificates);
        chain.ChainPolicy.ExtraStore.AddRange(policy.CustomRoots);
        bool built = chain.Build(certificate);
        if (chain.ChainElements.Count == 0)
            return failures | OdfMacroTrustFailure.CertificateChain;

        failures |= EvaluateChainStatuses(chain, policy.Mode == OdfMacroTrustMode.CustomRoot);

        X509Certificate2 terminal = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        if (policy.Mode == OdfMacroTrustMode.CustomRoot)
        {
            bool matchesRoot = policy.CustomRoots.Cast<X509Certificate2>()
                .Any(root => root.RawData.SequenceEqual(terminal.RawData));
            if (!matchesRoot)
                failures |= OdfMacroTrustFailure.CertificateChain;
        }
        else if (!built)
        {
            failures |= OdfMacroTrustFailure.CertificateChain;
        }

        return failures;
    }

    private static OdfMacroTrustFailure EvaluateSignerIdentity(
        X509Certificate2 certificate,
        OdfMacroTrustPolicy policy)
    {
        OdfMacroTrustFailure failures = OdfMacroTrustFailure.None;
        if (policy.AllowedSubjects.Count != 0 && !policy.AllowedSubjects.Contains(certificate.Subject))
            failures |= OdfMacroTrustFailure.Subject;
        if (policy.AllowedIssuers.Count != 0 && !policy.AllowedIssuers.Contains(certificate.Issuer))
            failures |= OdfMacroTrustFailure.Issuer;
        if (policy.AllowedEnhancedKeyUsages.Count != 0)
        {
            bool matches = certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .SelectMany(extension => extension.EnhancedKeyUsages.Cast<Oid>())
                .Any(oid => oid.Value is not null && policy.AllowedEnhancedKeyUsages.Contains(oid.Value));
            if (!matches)
                failures |= OdfMacroTrustFailure.EnhancedKeyUsage;
        }

        return failures;
    }

    private static OdfMacroTrustFailure EvaluateRevocation(
        X509Certificate2 certificate,
        OdfMacroTrustPolicy policy)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = ToX509RevocationMode(GetRevocationMode(policy));
        chain.ChainPolicy.VerificationTime = (policy.VerificationTime ?? DateTimeOffset.UtcNow).LocalDateTime;
        chain.ChainPolicy.ExtraStore.AddRange(policy.IntermediateCertificates);
        chain.ChainPolicy.ExtraStore.AddRange(policy.CustomRoots);
        _ = chain.Build(certificate);
        return EvaluateChainStatuses(chain, allowUntrustedRoot: true) & OdfMacroTrustFailure.Revocation;
    }

    private static OdfMacroTrustFailure EvaluateChainStatuses(X509Chain chain, bool allowUntrustedRoot)
    {
        OdfMacroTrustFailure failures = OdfMacroTrustFailure.None;
        foreach (X509ChainStatus status in chain.ChainStatus)
        {
            X509ChainStatusFlags flag = status.Status;
            if (allowUntrustedRoot)
                flag &= ~X509ChainStatusFlags.UntrustedRoot;
            if ((flag & (X509ChainStatusFlags.Revoked |
                    X509ChainStatusFlags.RevocationStatusUnknown |
                    X509ChainStatusFlags.OfflineRevocation)) != 0)
            {
                failures |= OdfMacroTrustFailure.Revocation;
                flag &= ~(X509ChainStatusFlags.Revoked |
                    X509ChainStatusFlags.RevocationStatusUnknown |
                    X509ChainStatusFlags.OfflineRevocation);
            }
            if (flag != X509ChainStatusFlags.NoError)
                failures |= OdfMacroTrustFailure.CertificateChain;
        }
        return failures;
    }

    private static OdfMacroRevocationMode GetRevocationMode(OdfMacroTrustPolicy policy) =>
        policy.CheckRevocation ? OdfMacroRevocationMode.Online : policy.RevocationMode;

    private static X509RevocationMode ToX509RevocationMode(OdfMacroRevocationMode mode) =>
        mode switch
        {
            OdfMacroRevocationMode.Online => X509RevocationMode.Online,
            OdfMacroRevocationMode.OfflineCache => X509RevocationMode.Offline,
            _ => X509RevocationMode.NoCheck
        };

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
