using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Provides ODF version compatibility diagnostics for document saves.
/// 提供文件儲存作業使用的 ODF 版本相容性診斷。
/// </summary>
public abstract partial class OdfDocument
{
    /// <summary>
    /// Gets the compatibility report produced for the most recent version-targeted save.
    /// 取得最近一次指定版本儲存作業所產生的相容性報告。
    /// </summary>
    /// <remarks>
    /// The property is <see langword="null"/> when the most recent save did not request a target version.
    /// 若最近一次儲存未要求目標版本，此屬性為 <see langword="null"/>。
    /// </remarks>
    public OdfVersionCompatibilityReport? LastVersionCompatibilityReport { get; private set; }

    /// <summary>
    /// Analyzes whether the current document can be represented by a target ODF version without semantic loss.
    /// 分析目前文件是否能在不損失語意的情況下由目標 ODF 版本表示。
    /// </summary>
    /// <param name="targetVersion">The target ODF version. / 目標 ODF 版本。</param>
    /// <returns>The structured compatibility report. / 結構化相容性報告。</returns>
    public OdfVersionCompatibilityReport AnalyzeVersionCompatibility(OdfVersion targetVersion) =>
        OdfVersionCompatibilityAnalyzer.Analyze(
            targetVersion,
            ("content.xml", GetContentXmlForPersistence()),
            ("styles.xml", StylesDom),
            ("meta.xml", MetaDom),
            ("settings.xml", SettingsDom));

    internal void SetLastVersionCompatibilityReport(OdfVersionCompatibilityReport? report) =>
        LastVersionCompatibilityReport = report;
}
