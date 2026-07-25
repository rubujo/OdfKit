using System.Text;
using System.Xml.Linq;

namespace OdfKit.Tests;

/// <summary>
/// Resolves MSBuild <c>&lt;Import Project="..."/&gt;</c> references when contract tests need to
/// inspect effective project metadata, so relocating shared properties into an imported
/// <c>.props</c> file (e.g. <c>eng/OdfKit.Package.props</c>) does not silently make
/// presence/value assertions blind to the data they are meant to guard.
/// 供契約測試在檢視專案有效中繼資料時解析 MSBuild <c>&lt;Import Project="..."/&gt;</c>，避免共用
/// 屬性搬進被匯入的 <c>.props</c> 檔（例如 <c>eng/OdfKit.Package.props</c>）後，存在性／數值
/// 斷言對其原本要把關的資料視而不見。
/// </summary>
internal static class CsprojImportResolver
{
    /// <summary>
    /// Reads the raw text of a project file together with the raw text of every (transitively)
    /// imported file, for simple substring contract checks (<c>Assert.Contains</c>) that must not
    /// miss content that has been relocated into an import. Import paths are resolved relative to
    /// the directory of the file that references them, matching MSBuild semantics.
    /// 讀取專案檔的原始文字，並串接其（遞迴）匯入檔案的原始文字，供簡單子字串契約檢查
    /// （<c>Assert.Contains</c>）使用，避免內容搬進 Import 後被漏檢。Import 路徑依參照它的檔案
    /// 所在目錄解析，與 MSBuild 語意一致。
    /// </summary>
    public static string ReadProjectTextWithImports(string repoRoot, string projectRelativePath)
    {
        string projectPath = Path.Combine(repoRoot, projectRelativePath);
        return ReadProjectTextWithImports(projectPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the effective value of a top-level, unconditioned MSBuild property by evaluating
    /// the project's own <c>PropertyGroup</c> elements together with those of every (transitively)
    /// imported file, in document order — a later definition overrides an earlier one, mirroring
    /// real MSBuild evaluation for the simple properties these contract tests assert on. Returns
    /// <see langword="null"/> when the property is not defined anywhere in the import chain, so a
    /// genuine regression (metadata removed from both the project and its imports) still fails the
    /// caller's assertion instead of being masked.
    /// 依文件順序求值專案本身與其（遞迴）匯入檔案的頂層無條件 <c>PropertyGroup</c>，回傳屬性的
    /// 有效值；後定義覆蓋前定義，對這些契約測試所斷言的簡單屬性而言，行為近似真正的 MSBuild
    /// 求值。當屬性在整個匯入鏈中都未定義時回傳 <see langword="null"/>，因此真實的退化
    /// （中繼資料同時從專案與其匯入檔案消失）仍會讓呼叫端斷言失敗，而不會被掩蓋。
    /// </summary>
    public static string? GetEffectivePropertyValue(string repoRoot, string projectRelativePath, string propertyName)
    {
        string projectPath = Path.Combine(repoRoot, projectRelativePath);
        string? value = null;
        CollectPropertyValue(projectPath, propertyName, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ref value);
        return value;
    }

    private static string ReadProjectTextWithImports(string projectPath, HashSet<string> visited)
    {
        string fullPath = Path.GetFullPath(projectPath);
        if (!visited.Add(fullPath) || !File.Exists(fullPath))
        {
            return string.Empty;
        }

        string text = File.ReadAllText(fullPath);
        var builder = new StringBuilder(text);
        string? directory = Path.GetDirectoryName(fullPath);

        foreach (string importProject in EnumerateImportTargets(text))
        {
            if (directory is null)
            {
                continue;
            }

            string importPath = Path.Combine(directory, importProject);
            builder.Append('\n');
            builder.Append(ReadProjectTextWithImports(importPath, visited));
        }

        return builder.ToString();
    }

    private static void CollectPropertyValue(string projectPath, string propertyName, HashSet<string> visited, ref string? value)
    {
        string fullPath = Path.GetFullPath(projectPath);
        if (!visited.Add(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        XDocument document = XDocument.Load(fullPath);
        XElement? root = document.Root;
        if (root is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(fullPath);

        // Walk root's direct children in document order so an <Import> is evaluated at the
        // position it appears — later PropertyGroup definitions (own or imported) override
        // earlier ones, matching MSBuild's linear evaluation for the unconditioned metadata these
        // tests assert on. Condition evaluation is intentionally out of scope.
        foreach (XElement child in root.Elements())
        {
            if (string.Equals(child.Name.LocalName, "Import", StringComparison.Ordinal))
            {
                string? importProject = child.Attribute("Project")?.Value;
                if (!string.IsNullOrWhiteSpace(importProject) && directory is not null)
                {
                    CollectPropertyValue(Path.Combine(directory, importProject), propertyName, visited, ref value);
                }
            }
            else if (string.Equals(child.Name.LocalName, "PropertyGroup", StringComparison.Ordinal))
            {
                XElement? property = child.Elements()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, propertyName, StringComparison.Ordinal));
                if (property is not null)
                {
                    value = property.Value;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateImportTargets(string projectText)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(projectText);
        }
        catch (System.Xml.XmlException)
        {
            yield break;
        }

        foreach (XElement import in document.Descendants().Where(e => string.Equals(e.Name.LocalName, "Import", StringComparison.Ordinal)))
        {
            string? importProject = import.Attribute("Project")?.Value;
            if (!string.IsNullOrWhiteSpace(importProject))
            {
                yield return importProject;
            }
        }
    }
}
