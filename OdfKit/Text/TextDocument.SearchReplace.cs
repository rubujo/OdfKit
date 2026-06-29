using System;
using System.Text.RegularExpressions;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Search & Replace with Actions/Regex

    /// <summary>
    /// Provides replace text.
    /// 搜尋指定文字並替換為新文字。
    /// </summary>
    /// <param name="search">The value to use. / 要搜尋的關鍵字</param>
    /// <param name="replacement">The text or value. / 要替換的新文字</param>
    /// <param name="styleAction">The value to use. / 套用於替換後文字片段的樣式委派作業</param>
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
    /// Provides replace text.
    /// 以規則運算式搜尋文字並替換為新文字。
    /// </summary>
    /// <param name="regex">The value to use. / 代表搜尋條件的規則運算式物件</param>
    /// <param name="replacement">The text or value. / 要替換的新文字</param>
    /// <param name="styleAction">The value to use. / 套用於替換後文字片段的樣式委派作業</param>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction = null) =>
        TextDocumentSearchReplaceEngine.ReplaceText(this, regex, replacement, styleAction);

    #endregion
}
