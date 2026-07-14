using System;
using System.Collections.Generic;

namespace OdfKit.Styles;

/// <summary>
/// Provides the OdfFontSegmenter API.
/// 提供針對 CNS 11643 中文標準交換碼字型之文字分段與對應工具。
/// </summary>
/// <remarks>
/// All members forward to <see cref="OdfFontContext.Default"/>; create an <see cref="OdfFontContext"/>
/// instance for isolated (for example per-tenant) plane font mappings.
/// 所有成員一律轉發至 <see cref="OdfFontContext.Default"/>；需要隔離的平面字型對應（例如各租戶）
/// 請改建立 <see cref="OdfFontContext"/> 執行個體。
/// </remarks>
public static class OdfFontSegmenter
{
    internal const string DefaultBaseFontFamily = "TW-Kai";

    /// <summary>
    /// Registers a custom supplementary-plane font mapping that takes precedence over the built-in rules.
    /// 註冊自訂的增補平面字型對應規則，查詢時優先於內建對應規則。
    /// </summary>
    /// <remarks>
    /// Later registrations are consulted first. When <paramref name="baseFontPattern"/> matches the base font family
    /// (ordinal, case-insensitive substring comparison), the mapping exclusively decides the result: planes missing from
    /// <paramref name="planeFontNames"/> keep the base font family and the built-in rules are not consulted.
    /// This method is thread-safe and registers on <see cref="OdfFontContext.Default"/>.
    /// 後註冊的規則優先比對。當 <paramref name="baseFontPattern"/> 與基礎字型家族名稱相符（不分大小寫的序數子字串比對）時，
    /// 該規則獨占決定結果：未列於 <paramref name="planeFontNames"/> 的平面維持基礎字型家族，且不再套用內建規則。
    /// 此方法為執行緒安全，並註冊於 <see cref="OdfFontContext.Default"/>。
    /// </remarks>
    /// <param name="baseFontPattern">The substring matched against the base font family name. / 用於比對基礎字型家族名稱的子字串。</param>
    /// <param name="planeFontNames">The mapping from Unicode plane number (1 to 16) to the font name to use. / Unicode 平面編號（1 至 16）對應至所用字型名稱的對照表。</param>
    /// <returns>A handle that removes the registration when disposed. / 釋放時移除此註冊的資源控制代碼。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="baseFontPattern"/> 為空或 <paramref name="planeFontNames"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentOutOfRangeException">當平面編號不在 1 至 16 範圍內時擲出</exception>
    /// <exception cref="ArgumentException">當任一平面對應的字型名稱為空白時擲出</exception>
    public static IDisposable RegisterSupplementaryPlaneFontMapping(string baseFontPattern, IReadOnlyDictionary<int, string> planeFontNames)
        => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(baseFontPattern, planeFontNames);

    /// <summary>
    /// Performs segment text.
    /// 將文字依照 Unicode 字面拆分為多個文字片段，並指派適當的字型名稱。
    /// </summary>
    /// <param name="text">要分段的來源文字</param>
    /// <param name="defaultFontName">預設的字型名稱</param>
    /// <returns>文字片段與字型名稱的 Tuple 集合</returns>
    public static List<(string Text, string FontName)> SegmentText(string text, string defaultFontName)
        => OdfFontContext.Default.SegmentText(text, defaultFontName);

    /// <summary>
    /// Gets supplementary plane font name.
    /// 依據基礎字型名稱與 Unicode 平面，取得對應的字型名稱（支援全字庫、花園明朝與字雲等增補平面與罕見字字型）。
    /// </summary>
    /// <param name="baseFontFamily">基礎字型名稱</param>
    /// <param name="plane">Unicode 平面（Plane）</param>
    /// <returns>對應的字型名稱</returns>
    public static string GetSupplementaryPlaneFontName(string baseFontFamily, int plane)
        => OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFontFamily, plane);

    /// <summary>
    /// 內建的增補平面字型對應規則（與 <see cref="OdfFontContext"/> 執行個體無關，所有情境共用）。
    /// </summary>
    internal static string GetBuiltInSupplementaryPlaneFontName(string baseFontFamily, int plane)
    {
        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = DefaultBaseFontFamily;

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
}
