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

    #endregion
}
