using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    #region Search & Replace with Actions/Regex

    /// <summary>
    /// Searches for the specified text and replaces it with new text.
    /// 搜尋指定文字並替換為新文字。
    /// </summary>
    public new OdfTextReplaceResult ReplaceText(string search, string replacement)
    {
        IReadOnlyList<OdfTextMatch> matches = FindText(search);
        base.ReplaceText(search, replacement);
        return new OdfTextReplaceResult(matches);
    }

    /// <summary>
    /// Finds exact text without exposing XML selectors.
    /// 尋找精確文字，且不暴露 XML selector。
    /// </summary>
    /// <param name="search">The text to find. / 要尋找的文字。</param>
    /// <returns>The matching text locations. / 符合的文字位置。</returns>
    public IReadOnlyList<OdfTextMatch> FindText(string search) => FindText(search, OdfTextQueryOptions.Default);

    /// <summary>
    /// Finds text using typed query options without exposing XML selectors.
    /// 使用具型別查詢選項尋找文字，且不暴露 XML selector。
    /// </summary>
    /// <param name="search">The text to find. / 要尋找的文字。</param>
    /// <param name="options">The typed query options. / 具型別查詢選項。</param>
    /// <returns>The matching text locations. / 符合的文字位置。</returns>
    public IReadOnlyList<OdfTextMatch> FindText(string search, OdfTextQueryOptions? options)
    {
        if (string.IsNullOrEmpty(search))
            throw new ArgumentException(null, nameof(search));
        options ??= OdfTextQueryOptions.Default;
        if (options.MaxResults < 0)
            throw new ArgumentOutOfRangeException(nameof(options));

        string text = BodyTextRoot.TextContent;
        var matches = new List<OdfTextMatch>();
        StringComparison comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int startIndex = 0;
        while (matches.Count < options.MaxResults)
        {
            int index = text.IndexOf(search, startIndex, comparison);
            if (index < 0)
                break;
            if (!options.WholeWord || IsWholeWord(text, index, search.Length))
                matches.Add(new OdfTextMatch(index, search.Length, text.Substring(index, search.Length)));
            startIndex = index + search.Length;
        }
        return matches;
    }

    /// <summary>
    /// Fills typed placeholders in this text document.
    /// 填入此文字文件中的具型別占位符。
    /// </summary>
    /// <param name="values">The placeholder values. / 占位符值。</param>
    /// <returns>The template binding report. / 範本繫結報告。</returns>
    public OdfTemplateBindReport FillTemplate(IReadOnlyDictionary<string, object?> values) =>
        TemplateBinder.Bind(this, values, OdfTemplateBindOptions.Default);

    /// <summary>
    /// Full overload of ReplaceText that accepts search, replacement, and styleAction.
    /// ReplaceText 完整多載：接受 search、replacement 與 styleAction。
    /// </summary>
    public void ReplaceText(string search, string replacement, Action<OdfTextRun>? styleAction)
    {
        if (styleAction is null)
        {
            base.ReplaceText(search, replacement);
        }
        else
        {
            TextDocumentSearchReplaceEngine.ReplaceText(this, search, replacement, styleAction);
        }
    }

    /// <summary>
    /// Searches for text using a regular expression and replaces it with new text.
    /// 以規則運算式搜尋文字並替換為新文字。
    /// </summary>
    public void ReplaceText(Regex regex, string replacement) => ReplaceText(regex, replacement, null);

    /// <summary>
    /// Short overload of ReplaceText that accepts regex, replacement, and styleAction; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 regex、replacement 與 styleAction；其餘可選參數使用預設值並轉呼叫最長 ReplaceText 多載。
    /// </summary>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction) =>
        TextDocumentSearchReplaceEngine.ReplaceText(this, regex, replacement, styleAction);

    private static bool IsWholeWord(string text, int index, int length)
    {
        bool leftBoundary = index == 0 || !IsWordCharacter(text[index - 1]);
        int endIndex = index + length;
        bool rightBoundary = endIndex == text.Length || !IsWordCharacter(text[endIndex]);
        return leftBoundary && rightBoundary;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    #endregion
}
