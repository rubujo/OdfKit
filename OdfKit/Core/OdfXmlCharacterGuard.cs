using System;
using System.Globalization;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// ODF 串流寫入路徑的輕量 XML 1.0 字元合法性防線（ODS／ODT 等共用）。
/// 寫入器將 <see cref="System.Xml.XmlWriterSettings.CheckCharacters"/> 關閉以降低熱迴圈成本後，
/// 由此類別在寫入前對使用者提供的文字與屬性值做等價的合法性檢查，
/// 確保偵測到非法字元時仍然快速失敗，而不是靜默寫出毀損的 XML。
/// </summary>
internal static class OdfXmlCharacterGuard
{
    /// <summary>
    /// 驗證文字是否僅含 XML 1.0 合法字元：#x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
    /// 以及以合法代理對表示的 [#x10000-#x10FFFF]；拒絕其餘控制字元、孤立代理與 #xFFFE/#xFFFF。
    /// 採簡單範圍比較（非 SearchValues），因為合法區間僅少數連續範圍且需配對代理，
    /// 逐字元比較在兩個 TFM（net10.0 與 netstandard2.0）皆可共用同一份程式碼且成本極低。
    /// </summary>
    /// <param name="value">要驗證的文字。</param>
    /// <param name="paramName">擲出例外時回報的參數名稱。</param>
    internal static void ValidateText(ReadOnlySpan<char> value, string paramName)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            // 快速通過：XML 1.0 合法且非代理的最常用區間 [#x20, #xD7FF]。
            if ((uint)(c - 0x20) <= 0xD7FF - 0x20)
                continue;

            if (c is '\t' or '\n' or '\r')
                continue;

            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    i++; // 合法代理對，一併跳過低代理。
                    continue;
                }

                // 孤立高代理（缺少後續低代理）不合法。
                throw CreateInvalidCharacterException(c, i, paramName);
            }

            // 私用區與其餘 BMP 合法區間 [#xE000, #xFFFD]；
            // 孤立低代理（0xDC00-0xDFFF）與 0xFFFE/0xFFFF 會落到擲出例外的路徑。
            if (c >= 0xE000 && c <= 0xFFFD)
                continue;

            throw CreateInvalidCharacterException(c, i, paramName);
        }
    }

    private static ArgumentException CreateInvalidCharacterException(char c, int index, string paramName) =>
        new(
            OdfLocalizer.GetMessage(
                "Err_OdfStreamWriter_InvalidXmlCharacter",
                string.Format(CultureInfo.InvariantCulture, "U+{0:X4}", (int)c),
                index),
            paramName);
}
