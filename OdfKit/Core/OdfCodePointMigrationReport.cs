using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OdfKit.Core;

/// <summary>
/// Describes the results of migrating text code points in an ODF document.
/// 描述 ODF 文件文字碼位遷移的結果。
/// </summary>
public sealed class OdfCodePointMigrationReport
{
    /// <summary>
    /// Gets the total number of code point replacements.
    /// 取得碼位替換總次數。
    /// </summary>
    public int TotalReplacements { get; }

    /// <summary>
    /// Gets the number of text nodes affected by the migration.
    /// 取得受遷移影響的文字節點數量。
    /// </summary>
    public int AffectedTextNodes { get; }

    /// <summary>
    /// Gets the replacement count for each source code point.
    /// 取得各來源碼位的替換次數。
    /// </summary>
    public IReadOnlyDictionary<int, int> ReplacementsByCodePoint { get; }

    internal OdfCodePointMigrationReport(
        int totalReplacements,
        int affectedTextNodes,
        IDictionary<int, int> replacementsByCodePoint)
    {
        TotalReplacements = totalReplacements;
        AffectedTextNodes = affectedTextNodes;
        ReplacementsByCodePoint = new ReadOnlyDictionary<int, int>(
            new Dictionary<int, int>(replacementsByCodePoint));
    }
}
