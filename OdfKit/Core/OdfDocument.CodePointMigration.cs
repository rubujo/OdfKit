using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// Provides Unicode code-point migration operations for ODF documents.
/// 提供 ODF 文件的 Unicode 碼位遷移操作。
/// </summary>
public abstract partial class OdfDocument
{
    /// <summary>
    /// Replaces mapped Unicode code points in all text nodes of the content and styles trees.
    /// 替換內容樹與樣式樹所有文字節點中已對應的 Unicode 碼位。
    /// </summary>
    /// <param name="codePointMapping">The source-to-target Unicode code point mapping. / 來源至目標 Unicode 碼位對應表。</param>
    /// <returns>A report describing the replacements performed. / 描述已執行替換的報告。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codePointMapping"/> is <see langword="null"/>. / <paramref name="codePointMapping"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">A mapping key or value is not a valid Unicode scalar value. / 對應表的鍵或值不是有效的 Unicode 純量值。</exception>
    /// <remarks>
    /// This API migrates legacy CNS PUA characters to assigned Unicode code points. Identity mappings are ignored.
    /// 此 API 用於將舊版全字庫 PUA 自造字遷移至已正式指派的 Unicode 碼位。相同來源與目標的對應項目會被略過。
    /// </remarks>
    public OdfCodePointMigrationReport MigrateTextCodePoints(IReadOnlyDictionary<int, int> codePointMapping)
    {
        if (codePointMapping is null)
        {
            throw new ArgumentNullException(nameof(codePointMapping));
        }

        var mapping = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> pair in codePointMapping)
        {
            if (!IsUnicodeScalar(pair.Key) || !IsUnicodeScalar(pair.Value))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfCodePointMigration_MappingCodePointInvalid"),
                    nameof(codePointMapping));
            }

            if (pair.Key != pair.Value)
            {
                mapping[pair.Key] = pair.Value;
            }
        }

        var replacements = new Dictionary<int, int>();
        if (mapping.Count == 0)
        {
            return new OdfCodePointMigrationReport(0, 0, replacements);
        }

        int totalReplacements = 0;
        int affectedTextNodes = 0;
        MigrateTree(ContentDom, mapping, replacements, ref totalReplacements, ref affectedTextNodes);
        MigrateTree(StylesDom, mapping, replacements, ref totalReplacements, ref affectedTextNodes);

        return new OdfCodePointMigrationReport(totalReplacements, affectedTextNodes, replacements);
    }

    private static void MigrateTree(
        OdfNode node,
        IReadOnlyDictionary<int, int> mapping,
        IDictionary<int, int> replacements,
        ref int totalReplacements,
        ref int affectedTextNodes)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (ContainsMappedCodePoint(text, mapping))
            {
                node.TextContent = ReplaceMappedCodePoints(text, mapping, replacements, ref totalReplacements);
                affectedTextNodes++;
            }

            return;
        }

        foreach (OdfNode child in node.Children)
        {
            MigrateTree(child, mapping, replacements, ref totalReplacements, ref affectedTextNodes);
        }
    }

    private static bool ContainsMappedCodePoint(string text, IReadOnlyDictionary<int, int> mapping)
    {
        for (int index = 0; index < text.Length; index++)
        {
            int codePoint = ReadCodePoint(text, index, out int charCount);
            if (mapping.ContainsKey(codePoint))
            {
                return true;
            }

            index += charCount - 1;
        }

        return false;
    }

    private static string ReplaceMappedCodePoints(
        string text,
        IReadOnlyDictionary<int, int> mapping,
        IDictionary<int, int> replacements,
        ref int totalReplacements)
    {
        var builder = new StringBuilder(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            int codePoint = ReadCodePoint(text, index, out int charCount);
            if (mapping.TryGetValue(codePoint, out int replacement))
            {
                builder.Append(char.ConvertFromUtf32(replacement));
                replacements.TryGetValue(codePoint, out int count);
                replacements[codePoint] = count + 1;
                totalReplacements++;
            }
            else
            {
                builder.Append(text, index, charCount);
            }

            index += charCount - 1;
        }

        return builder.ToString();
    }

    private static int ReadCodePoint(string text, int index, out int charCount)
    {
        char current = text[index];
        if (char.IsHighSurrogate(current) &&
            index + 1 < text.Length &&
            char.IsLowSurrogate(text[index + 1]))
        {
            charCount = 2;
            return char.ConvertToUtf32(current, text[index + 1]);
        }

        charCount = 1;
        return current;
    }

    private static bool IsUnicodeScalar(int value) =>
        value is >= 0 and <= 0x10FFFF && value is not (>= 0xD800 and <= 0xDFFF);
}
