using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.Styles;

/// <summary>
/// Provides the OdfFontSegmenter API.
/// 提供針對 CNS 11643 中文標準交換碼字型之文字分段與對應工具。
/// </summary>
public static class OdfFontSegmenter
{
    private const string DefaultBaseFontFamily = "TW-Kai";

    // 自訂平面對應規則的不可變快照：讀取端（分段熱路徑）只做一次 volatile 讀取即可安全巡覽，
    // 完全不需要鎖；寫入端（註冊／解除註冊）以 _planeMappingLock 序列化後整個換掉陣列參考。
    private static readonly object _planeMappingLock = new();
    private static volatile PlaneFontMappingRegistration[] _customPlaneMappings = [];

    /// <summary>
    /// Registers a custom supplementary-plane font mapping that takes precedence over the built-in rules.
    /// 註冊自訂的增補平面字型對應規則，查詢時優先於內建對應規則。
    /// </summary>
    /// <remarks>
    /// Later registrations are consulted first. When <paramref name="baseFontPattern"/> matches the base font family
    /// (ordinal, case-insensitive substring comparison), the mapping exclusively decides the result: planes missing from
    /// <paramref name="planeFontNames"/> keep the base font family and the built-in rules are not consulted.
    /// This method is thread-safe.
    /// 後註冊的規則優先比對。當 <paramref name="baseFontPattern"/> 與基礎字型家族名稱相符（不分大小寫的序數子字串比對）時，
    /// 該規則獨占決定結果：未列於 <paramref name="planeFontNames"/> 的平面維持基礎字型家族，且不再套用內建規則。
    /// 此方法為執行緒安全。
    /// </remarks>
    /// <param name="baseFontPattern">The substring matched against the base font family name. / 用於比對基礎字型家族名稱的子字串。</param>
    /// <param name="planeFontNames">The mapping from Unicode plane number (1 to 16) to the font name to use. / Unicode 平面編號（1 至 16）對應至所用字型名稱的對照表。</param>
    /// <returns>A handle that removes the registration when disposed. / 釋放時移除此註冊的資源控制代碼。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="baseFontPattern"/> 為空或 <paramref name="planeFontNames"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentOutOfRangeException">當平面編號不在 1 至 16 範圍內時擲出</exception>
    /// <exception cref="ArgumentException">當任一平面對應的字型名稱為空白時擲出</exception>
    public static IDisposable RegisterSupplementaryPlaneFontMapping(string baseFontPattern, IReadOnlyDictionary<int, string> planeFontNames)
    {
        if (string.IsNullOrEmpty(baseFontPattern))
            throw new ArgumentNullException(nameof(baseFontPattern));
        if (planeFontNames is null)
            throw new ArgumentNullException(nameof(planeFontNames));

        // 防禦性複製：呼叫端後續修改原字典不得影響已註冊規則；複製後的字典不再變動，可供多執行緒無鎖讀取。
        var planeFonts = new Dictionary<int, string>(planeFontNames.Count);
        foreach (KeyValuePair<int, string> pair in planeFontNames)
        {
            if (pair.Key is < 1 or > 16)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(planeFontNames),
                    pair.Key,
                    OdfLocalizer.GetMessage("Err_OdfFontSegmenter_PlaneOutOfRange"));
            }

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfFontSegmenter_PlaneFontNameEmpty"),
                    nameof(planeFontNames));
            }

            planeFonts[pair.Key] = pair.Value;
        }

        var registration = new PlaneFontMappingRegistration(baseFontPattern, planeFonts);
        lock (_planeMappingLock)
        {
            PlaneFontMappingRegistration[] current = _customPlaneMappings;
            var next = new PlaneFontMappingRegistration[current.Length + 1];
            next[0] = registration; // 最新註冊排最前，讓後註冊者可覆蓋先前規則
            Array.Copy(current, 0, next, 1, current.Length);
            _customPlaneMappings = next;
        }

        return registration;
    }

    /// <summary>
    /// Performs segment text.
    /// 將文字依照 Unicode 字面拆分為多個文字片段，並指派適當的字型名稱。
    /// </summary>
    /// <param name="text">要分段的來源文字</param>
    /// <param name="defaultFontName">預設的字型名稱</param>
    /// <returns>文字片段與字型名稱的 Tuple 集合</returns>
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

            if (plane >= 1 && GetCustomPlaneFontName(defaultFontName, plane) is string customFont)
            {
                // 自訂規則可涵蓋任何增補平面（含 Plane 1 SMP 與未來新增區塊）。
                targetFont = customFont;
            }
            else if (plane == 2 || plane == 3 || plane == 15 || plane == 16)
            {
                // 內建規則維持既有行為：僅處理 CJK 罕字實際使用的平面。
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
    /// Gets supplementary plane font name.
    /// 依據基礎字型名稱與 Unicode 平面，取得對應的字型名稱（支援全字庫、花園明朝與字雲等增補平面與罕見字字型）。
    /// </summary>
    /// <param name="baseFontFamily">基礎字型名稱</param>
    /// <param name="plane">Unicode 平面（Plane）</param>
    /// <returns>對應的字型名稱</returns>
    public static string GetSupplementaryPlaneFontName(string baseFontFamily, int plane)
    {
        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = DefaultBaseFontFamily;

        // 0. 自訂註冊規則優先於所有內建對應
        if (GetCustomPlaneFontName(baseFontFamily, plane) is string customFontName)
        {
            return customFontName;
        }

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

        // 5. 支援 Windows 系統字型 MingLiU（細明體）／PMingLiU（新細明體）對照
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

        // 6. 支援 Windows 系統字型 SimSun（中易宋體）／NSimSun 對照
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

        // 其餘常規字型（如思源黑體、Noto Sans、微軟正黑體等）不進行任何拆分字型對照，直接傳回原字型
        return baseFontFamily;
    }

    /// <summary>
    /// 查詢自訂平面對應規則；命中規則時傳回對應字型名稱（該平面未設定時傳回基礎字型家族），無任何規則命中時傳回 null。
    /// </summary>
    private static string? GetCustomPlaneFontName(string baseFontFamily, int plane)
    {
        // 單次 volatile 讀取取得不可變快照；無註冊時以長度檢查快速返回，維持熱路徑零額外成本。
        PlaneFontMappingRegistration[] customMappings = _customPlaneMappings;
        if (customMappings.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = DefaultBaseFontFamily;

        foreach (PlaneFontMappingRegistration mapping in customMappings)
        {
            if (baseFontFamily.Contains(mapping.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.PlaneFonts.TryGetValue(plane, out string? fontName) ? fontName : baseFontFamily;
            }
        }

        return null;
    }

    private sealed class PlaneFontMappingRegistration(string pattern, Dictionary<int, string> planeFonts) : IDisposable
    {
        internal string Pattern { get; } = pattern;

        internal Dictionary<int, string> PlaneFonts { get; } = planeFonts;

        public void Dispose()
        {
            lock (_planeMappingLock)
            {
                PlaneFontMappingRegistration[] current = _customPlaneMappings;
                int index = Array.IndexOf(current, this);
                if (index < 0)
                {
                    // 已被移除（重複 Dispose）：直接返回即可，維持冪等性。
                    return;
                }

                var next = new PlaneFontMappingRegistration[current.Length - 1];
                Array.Copy(current, 0, next, 0, index);
                Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                _customPlaneMappings = next;
            }
        }
    }
}
