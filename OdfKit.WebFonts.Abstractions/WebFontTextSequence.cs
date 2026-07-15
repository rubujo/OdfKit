using OdfKit.Compliance;

namespace OdfKit.WebFonts;

/// <summary>
/// Preserves one Unicode scalar sequence for IVS, shaping clusters, PUA, and supplementary characters.
/// 保留單一 Unicode 純量值序列，以支援 IVS、塑形 cluster、PUA 與補充字元。
/// </summary>
public sealed class WebFontTextSequence
{
    private WebFontTextSequence(string text, int[] unicodeScalars)
    {
        Text = text;
        UnicodeScalars = unicodeScalars;
    }

    /// <summary>
    /// Gets the original UTF-16 text without reordering or normalization.
    /// 取得未重新排序或正規化的原始 UTF-16 文字。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the ordered Unicode scalar values represented by the original text.
    /// 取得原始文字所表示且順序不變的 Unicode 純量值。
    /// </summary>
    public IReadOnlyList<int> UnicodeScalars { get; }

    /// <summary>
    /// Creates a sequence while rejecting unpaired UTF-16 surrogates.
    /// 建立序列並拒絕未成對的 UTF-16 surrogate。
    /// </summary>
    /// <param name="text">The text whose scalar order must be preserved. / 必須保留純量值順序的文字。</param>
    /// <returns>A sequence containing the original text and ordered Unicode scalar values. / 包含原始文字與有序 Unicode 純量值的序列。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null. / 當 <paramref name="text"/> 為 null 時擲回。</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> contains an unpaired surrogate. / 當 <paramref name="text"/> 包含未成對 surrogate 時擲回。</exception>
    public static WebFontTextSequence Create(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(
                nameof(text),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        var scalars = new List<int>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                {
                    throw new ArgumentException(
                        OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"),
                        nameof(text));
                }

                scalars.Add(char.ConvertToUtf32(current, text[++index]));
            }
            else if (char.IsLowSurrogate(current))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"),
                    nameof(text));
            }
            else
            {
                scalars.Add(current);
            }
        }

        return new WebFontTextSequence(text, scalars.ToArray());
    }
}
