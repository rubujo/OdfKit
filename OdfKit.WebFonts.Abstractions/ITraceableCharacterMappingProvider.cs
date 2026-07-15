namespace OdfKit.WebFonts;

/// <summary>
/// Describes a mapping provider whose source, version, digest, and license are traceable.
/// 描述可追溯來源、版本、摘要與授權的 mapping provider。
/// </summary>
public interface ITraceableCharacterMappingProvider : ICharacterMappingProvider
{
    /// <summary>
    /// Gets the immutable upstream data version.
    /// 取得不可變的上游資料版本。
    /// </summary>
    string DataVersion { get; }

    /// <summary>
    /// Gets the first-party source URI.
    /// 取得第一方來源 URI。
    /// </summary>
    string SourceUri { get; }

    /// <summary>
    /// Gets the lowercase SHA-256 digest of the complete source archive or profile.
    /// 取得完整來源封存檔或 profile 的小寫 SHA-256 摘要。
    /// </summary>
    string SourceSha256 { get; }

    /// <summary>
    /// Gets the SPDX-compatible or deployment-defined license identifier.
    /// 取得 SPDX 相容或部署端定義的授權識別碼。
    /// </summary>
    string LicenseId { get; }

    /// <summary>
    /// Gets the required source attribution.
    /// 取得必要的來源標示。
    /// </summary>
    string Attribution { get; }
}
