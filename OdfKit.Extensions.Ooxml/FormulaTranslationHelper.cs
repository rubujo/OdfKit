using System;
using System.Text;

namespace OdfKit.Conversion;

/// <summary>
/// 公式翻譯的共用輔助方法：提供引號感知的區段轉換，
/// 確保工作表參照改寫不會波及公式內的字串常數（如 <c>="File.Name"</c>、<c>="Error!"</c>）。
/// </summary>
internal static class FormulaTranslationHelper
{
    /// <summary>
    /// 只對雙引號字串常數以外的公式片段套用 <paramref name="transform"/>；
    /// 字串常數（含 <c>""</c> 逸出的雙引號）原樣保留。
    /// </summary>
    internal static string ReplaceOutsideStringLiterals(string formula, Func<string, string> transform)
    {
        if (formula.IndexOf('"') < 0)
        {
            return transform(formula);
        }

        var builder = new StringBuilder(formula.Length + 8);
        int index = 0;
        while (index < formula.Length)
        {
            if (formula[index] == '"')
            {
                // 字串常數：掃描至結束引號（"" 視為逸出的雙引號）並原樣輸出
                int start = index;
                index++;
                while (index < formula.Length)
                {
                    if (formula[index] == '"')
                    {
                        if (index + 1 < formula.Length && formula[index + 1] == '"')
                        {
                            index += 2;
                            continue;
                        }

                        index++;
                        break;
                    }

                    index++;
                }

                builder.Append(formula, start, index - start);
            }
            else
            {
                int start = index;
                while (index < formula.Length && formula[index] != '"')
                {
                    index++;
                }

                builder.Append(transform(formula.Substring(start, index - start)));
            }
        }

        return builder.ToString();
    }
}
