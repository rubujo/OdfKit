using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit;

/// <summary>
/// Reports the result of a template binding operation.
/// 回報模板繫結作業的結果。
/// </summary>
public sealed class OdfTemplateBindReport
{
    /// <summary>
    /// Gets or sets the number of scalar replacements.
    /// 取得或設定純量替換數量。
    /// </summary>
    public int ReplacementCount { get; set; }

    /// <summary>
    /// Gets or sets the number of expanded collection items.
    /// 取得或設定已展開集合項目數量。
    /// </summary>
    public int ExpandedItemCount { get; set; }

    /// <summary>
    /// Gets or sets the number of changed cells or paragraphs.
    /// 取得或設定已變更的儲存格或段落數量。
    /// </summary>
    public int ChangedNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of image placeholders replaced.
    /// 取得或設定已替換的圖片占位符數量。
    /// </summary>
    public int ImageReplacementCount { get; set; }

    /// <summary>
    /// Gets unresolved placeholders and collection names.
    /// 取得未解析的占位符與集合名稱。
    /// </summary>
    public IList<string> UnresolvedPlaceholders { get; } = new List<string>();

    /// <summary>
    /// Gets unresolved placeholders with lightweight location hints.
    /// 取得包含輕量位置提示的未解析占位符。
    /// </summary>
    public IList<OdfTemplateUnresolvedPlaceholder> UnresolvedPlaceholderDetails { get; } = new List<OdfTemplateUnresolvedPlaceholder>();

    /// <summary>
    /// Gets the number of successful replacements grouped by placeholder expression.
    /// 取得依占位符運算式分組的成功替換次數。
    /// </summary>
    public IDictionary<string, int> PlaceholderHits { get; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the collection names expanded during binding.
    /// 取得繫結期間已展開的集合名稱。
    /// </summary>
    public IList<string> ExpandedCollections { get; } = new List<string>();

    /// <summary>
    /// Gets non-fatal binding warnings.
    /// 取得非致命的繫結警告。
    /// </summary>
    public IList<string> Warnings { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether every placeholder was resolved.
    /// 取得是否所有占位符皆已解析。
    /// </summary>
    public bool IsComplete => UnresolvedPlaceholders.Count == 0 && Warnings.Count == 0;

    /// <summary>
    /// Gets unresolved placeholders, expanded collections, and warnings as strongly typed diagnostics.
    /// 取得未解析占位符、展開集合與警告項目的強型別診斷。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        UnresolvedPlaceholderDetails
            .Select(detail => new OdfDiagnostic(
                "UnresolvedPlaceholder",
                OdfIssueSeverity.Warning,
                detail.Expression,
                objectId: detail.DocumentKind,
                location: detail.LocationHint))
            .Concat(OdfDiagnostic.FromStrings(ExpandedCollections, "ExpandedCollection", OdfIssueSeverity.Info))
            .Concat(OdfDiagnostic.FromStrings(Warnings, "Warning", OdfIssueSeverity.Warning))
            .ToList();
}
