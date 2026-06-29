using System;
using System.Text.RegularExpressions;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Search & Replace with Actions/Regex

    /// <summary>
    /// Searches for the specified text and replaces it with new text.
    /// 搜尋指定文字並替換為新文字。
    /// </summary>
    /// <param name="search">The keyword to search for. / 要搜尋的關鍵字。</param>
    /// <param name="replacement">The replacement text. / 要替換的新文字。</param>
    /// <param name="styleAction">The style delegate applied to replaced text runs. / 套用於替換後文字片段的樣式委派作業。</param>
    public void ReplaceText(string search, string replacement, Action<OdfTextRun>? styleAction = null)
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
    /// <param name="regex">The regular expression object representing the search condition. / 代表搜尋條件的規則運算式物件。</param>
    /// <param name="replacement">The replacement text. / 要替換的新文字。</param>
    /// <param name="styleAction">The style delegate applied to replaced text runs. / 套用於替換後文字片段的樣式委派作業。</param>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction = null) =>
        TextDocumentSearchReplaceEngine.ReplaceText(this, regex, replacement, styleAction);

    #endregion
}
