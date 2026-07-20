using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Encoding.Legacy;

/// <summary>
/// Strictly decodes Microsoft code page 950 Big5 data into Unicode.
/// 將 Microsoft code page 950 Big5 資料嚴格解碼為 Unicode。
/// </summary>
public sealed class Big5CharacterMappingProvider : ICharacterMappingProvider
{
    private static readonly System.Text.Encoding Big5Encoding = CreateEncoding();

    /// <summary>
    /// Gets the stable Big5 mapping profile identifier.
    /// 取得穩定的 Big5 對照 profile 識別碼。
    /// </summary>
    public string ProfileId => "big5-cp950";

    /// <summary>
    /// Decodes a complete strict Big5 byte sequence.
    /// 解碼完整且嚴格的 Big5 位元組序列。
    /// </summary>
    /// <param name="source">The Big5 bytes to decode. / 要解碼的 Big5 位元組。</param>
    /// <returns>The decoded Unicode text. / 解碼後的 Unicode 文字。</returns>
    public string Decode(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_WebFontLegacyEncoding_SourceRequired"));
        }

        return Big5Encoding.GetString(source);
    }

    internal static System.Text.Encoding GetStrictEncoding() => Big5Encoding;

    private static System.Text.Encoding CreateEncoding()
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(950, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
