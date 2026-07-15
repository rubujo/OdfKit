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

    /// <inheritdoc />
    public string ProfileId => "big5-cp950";

    /// <inheritdoc />
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
