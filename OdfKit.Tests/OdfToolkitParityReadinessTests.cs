using System;
using System.IO;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF Toolkit 對標文件與 corpus manifest 已宣告必要契約。
/// </summary>
public class OdfToolkitParityReadinessTests
{
    /// <summary>
    /// 驗證 parity 文件列出外部 validator 啟用方式與 mismatch 規則。
    /// </summary>
    [Fact]
    public void OdfToolkitParityDocumentDeclaresExternalBaselineContract()
    {
        string repoRoot = FindRepositoryRoot();
        string parity = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-toolkit-parity.md"));

        Assert.Contains("ODF Toolkit", parity, StringComparison.Ordinal);
        Assert.Contains("ODF Validator", parity, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_ODFVALIDATOR_JAR", parity, StringComparison.Ordinal);
        Assert.Contains("--baseline odf-validator", parity, StringComparison.Ordinal);
        Assert.Contains("validate-corpus", parity, StringComparison.Ordinal);
        Assert.Contains("baselineMismatchCount", parity, StringComparison.Ordinal);
        Assert.Contains("documented exception", parity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證 corpus manifest 宣告小型可提交 corpus 與外部 corpus 路徑規則。
    /// </summary>
    [Fact]
    public void CorpusManifestDeclaresFixtureMetadataContract()
    {
        string repoRoot = FindRepositoryRoot();
        string manifest = File.ReadAllText(Path.Combine(repoRoot, "docs", "corpus-manifest.md"));

        Assert.Contains("ODFKIT_PARITY_CORPUS_ROOT", manifest, StringComparison.Ordinal);
        Assert.Contains("license", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("roundTrip", manifest, StringComparison.Ordinal);
        Assert.Contains("validate-corpus", manifest, StringComparison.Ordinal);
        Assert.Contains("generated-format-minimal", manifest, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }
}
