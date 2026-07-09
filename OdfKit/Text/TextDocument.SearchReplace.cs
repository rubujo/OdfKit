using System;
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
    public new void ReplaceText(string search, string replacement) => ReplaceText(search, replacement, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
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
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction) =>
        TextDocumentSearchReplaceEngine.ReplaceText(this, regex, replacement, styleAction);

    #endregion
}
