using System.Collections.Generic;

namespace OdfKit.Text;

/// <summary>
/// Reports a task-oriented text replacement operation.
/// 回報任務導向文字取代作業。
/// </summary>
/// <param name="matches">The matches observed before replacement. / 取代前找到的符合項目。</param>
public sealed class OdfTextReplaceResult(IReadOnlyList<OdfTextMatch> matches)
{
    /// <summary>
    /// Gets the matches observed before replacement.
    /// 取得取代前找到的符合項目。
    /// </summary>
    public IReadOnlyList<OdfTextMatch> Matches { get; } = matches;

    /// <summary>
    /// Gets the replacement count.
    /// 取得取代次數。
    /// </summary>
    public int ReplacementCount => Matches.Count;
}
