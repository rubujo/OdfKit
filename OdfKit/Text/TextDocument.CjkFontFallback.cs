using System;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region CJK Font Fallback


    /// <summary>
    /// 套用中日韓（CJK）字型遞補設定。
    /// </summary>
    public void ApplyCjkFontFallback()
    {
        // 宣告預設的中日韓字型，若不存在則新增
        AddFontFace("PMingLiU", "PMingLiU", "system-serif", "variable");
        AddFontFace("Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable");
        AddFontFace("MS Mincho", "MS Mincho", "system-serif", "variable");
        AddFontFace("MS Gothic", "MS Gothic", "system-sans-serif", "variable");
        AddFontFace("SimSun", "SimSun", "system-serif", "variable");
        AddFontFace("Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable");
        AddFontFace("Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable");
    }


    #endregion
}
