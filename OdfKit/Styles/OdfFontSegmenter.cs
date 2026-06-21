using System;
using System.Collections.Generic;
using System.Text;

namespace OdfKit.Styles;

/// <summary>
/// 提供針對 CNS 11643 中文標準交換碼字型之文字分段與對應工具。
/// </summary>
public static class OdfFontSegmenter
{
    /// <summary>
    /// 將文字依照 Unicode 字面拆分為多個文字片段，並指派適當的字型名稱。
    /// </summary>
    /// <param name="text">要分段的來源文字。</param>
    /// <param name="defaultFontName">預設的字型名稱。</param>
    /// <returns>文字片段與字型名稱的 Tuple 集合。</returns>
    public static List<(string Text, string FontName)> SegmentText(string text, string defaultFontName)
    {
        var result = new List<(string Text, string FontName)>();
        if (string.IsNullOrEmpty(text))
            return result;

        int i = 0;
        int len = text.Length;
        var sb = new StringBuilder();
        string currentFont = defaultFontName;

        while (i < len)
        {
            int codePoint;
            int charCount;
            if (char.IsHighSurrogate(text[i]) && i + 1 < len && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                charCount = 2;
            }
            else
            {
                codePoint = text[i];
                charCount = 1;
            }

            int plane = codePoint >> 16;
            string targetFont = defaultFontName;

            if (plane == 2 || plane == 3 || plane == 15 || plane == 16)
            {
                targetFont = GetSupplementaryPlaneFontName(defaultFontName, plane);
            }

            if (targetFont != currentFont)
            {
                if (sb.Length > 0)
                {
                    result.Add((sb.ToString(), currentFont));
                    sb.Clear();
                }
                currentFont = targetFont;
            }

            if (charCount == 2)
            {
                sb.Append(text[i]);
                sb.Append(text[i + 1]);
            }
            else
            {
                sb.Append(text[i]);
            }

            i += charCount;
        }

        if (sb.Length > 0)
        {
            result.Add((sb.ToString(), currentFont));
        }

        return result;
    }

    /// <summary>
    /// 依據基礎字型名稱與 Unicode 平面，取得對應的字型名稱（支援全字庫、花園明朝與字雲等增補平面與罕見字字型）。
    /// </summary>
    /// <param name="baseFontFamily">基礎字型名稱。</param>
    /// <param name="plane">Unicode 平面（Plane）。</param>
    /// <returns>對應的字型名稱。</returns>
    public static string GetSupplementaryPlaneFontName(string baseFontFamily, int plane)
    {
        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = "TW-Kai";

        // 1. 支援全字庫正宋體 (TW-Song)
        if (baseFontFamily.Contains("TW-Song", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("全字庫正宋", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => "TW-Song-Ext-B-98_1",
                15 => "TW-Song-Plus-98_1",
                16 => "TW-Song-Plus-98_1",
                _ => "TW-Song-98_1"
            };
        }

        // 2. 支援全字庫正楷體與標楷體 (TW-Kai / DFKai-SB / BiauKai)
        if (baseFontFamily.Contains("TW-Kai", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("全字庫正楷", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("DFKai-SB", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("標楷", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("BiauKai", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => "TW-Kai-Ext-B-98_1",
                15 => "TW-Kai-Plus-98_1",
                16 => "TW-Kai-Plus-98_1",
                _ => "TW-Kai-98_1"
            };
        }

        // 3. 支援字雲 / Jigmo 字型對應
        if (baseFontFamily.Contains("Jigmo", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("字雲", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => "Jigmo2",
                3 => "Jigmo3",
                _ => "Jigmo"
            };
        }

        // 4. 支援花園明朝 (HanaMin) / Hanazono 字型對應
        if (baseFontFamily.Contains("HanaMin", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("Hanazono", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("花園", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => "HanaMinB",
                15 => "HanaMinB",
                16 => "HanaMinB",
                _ => "HanaMinA"
            };
        }

        // 5. 支援 Windows 系統字型 MingLiU (細明體) / PMingLiU (新細明體) 映射
        if (baseFontFamily.Contains("MingLiU", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("細明", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => baseFontFamily.Contains("PMingLiU", StringComparison.OrdinalIgnoreCase) || baseFontFamily.Contains("新細明", StringComparison.OrdinalIgnoreCase) ? "PMingLiU-ExtB"
                   : baseFontFamily.Contains("HKSCS", StringComparison.OrdinalIgnoreCase) ? "MingLiU_HKSCS-ExtB"
                   : "MingLiU-ExtB",
                3 => "SimSun-ExtG", // Windows 目前由 SimSun-ExtG 涵蓋 Plane 3
                _ => baseFontFamily
            };
        }

        // 6. 支援 Windows 系統字型 SimSun (中易宋體) / NSimSun 映射
        if (baseFontFamily.Contains("SimSun", StringComparison.OrdinalIgnoreCase) ||
            baseFontFamily.Contains("宋体", StringComparison.OrdinalIgnoreCase))
        {
            return plane switch
            {
                2 => "SimSun-ExtB",
                3 => "SimSun-ExtG",
                _ => baseFontFamily
            };
        }

        // 其餘常規字型（如思源黑體、Noto Sans、微軟正黑體等）不進行任何拆分字型映射，直接返回原字型
        return baseFontFamily;
    }
}
