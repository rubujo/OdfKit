using System.Text;
using OdfKit.WebFonts.Encoding.Legacy;

namespace OdfKit.WebFonts.Tests;

public sealed class LegacyEncodingTests
{
    [Fact]
    public void Big5Provider_StrictlyDecodesCp950()
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] bytes = System.Text.Encoding.GetEncoding(950).GetBytes("中文");

        string decoded = new Big5CharacterMappingProvider().Decode(bytes);

        Assert.Equal("中文", decoded);
    }

    [Fact]
    public void Big5EProvider_UsesExplicitSupplementaryMappingBeforeCp950()
    {
        using var reader = new StringReader("# Big5E direct mapping\n8140\t20000\n");
        Big5EMapping mapping = Big5EMapping.Load(reader, "cns-2024");

        string decoded = new Big5ECharacterMappingProvider(mapping).Decode([0x41, 0x81, 0x40]);

        Assert.Equal("A\U00020000", decoded);
    }

    [Fact]
    public void Big5EProvider_RejectsUnknownUserDefinedBytes()
    {
        using var reader = new StringReader("8140\t20000\n");
        Big5EMapping mapping = Big5EMapping.Load(reader, "cns-2024");

        Assert.Throws<DecoderFallbackException>(() =>
            new Big5ECharacterMappingProvider(mapping).Decode([0x81, 0x41]));
    }

    [Fact]
    public void PrivateUseProvider_IsScopedByProfile()
    {
        var provider = new PrivateUseCharacterMappingProvider(
            "agency-a-educ-v1",
            new Dictionary<string, int> { ["8140"] = 0xF0001 });

        Assert.Equal("agency-a-educ-v1", provider.ProfileId);
        Assert.Equal("\U000F0001", provider.Decode([0x81, 0x40]));
    }
}
