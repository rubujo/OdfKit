using System.Text;
using OdfKit.WebFonts.Profiles;

namespace OdfKit.WebFonts.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void JsonProfileDecodesMultibyteAndPrivateMappings()
    {
        const string json = """
              {
                "schemaVersion": 1,
                "profileId": "international-agency-v1",
                "dataVersion": "2026.07",
                "sourceUri": "https://example.invalid/mapping.json",
                "sourceSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "licenseId": "LicenseRef-Agency",
                "attribution": "Example agency mapping.",
                "mappings": {
                "8140": "𠀀",
                "8EA140": "󰀁"
              }
            }
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        JsonCharacterMappingProvider provider = JsonCharacterMappingProvider.Load(stream, 4096, 10);

        string decoded = provider.Decode([0x41, 0x81, 0x40, 0x8E, 0xA1, 0x40]);

        Assert.Equal("A𠀀󰀁", decoded);
        Assert.Equal("2026.07", provider.DataVersion);
        Assert.Equal("LicenseRef-Agency", provider.LicenseId);
    }

    [Fact]
    public void CnsProfileDecodesPlaneOneAndSupplementaryPlane()
    {
        using var planeOne = new StringReader("1-2121\t4E00\n");
        using var planeTwo = new StringReader("2-2121\t20000\n");
        Cns11643EucTwMappingProvider provider = Cns11643EucTwMappingProvider.Load(
            [planeOne, planeTwo],
            10);

        string decoded = provider.Decode([0x41, 0xA1, 0xA1, 0x8E, 0xA2, 0xA1, 0xA1]);

        Assert.Equal("A一𠀀", decoded);
        Assert.Equal("cns11643-euc-tw-2026-08-05", provider.ProfileId);
        Assert.Equal(Cns11643EucTwMappingProvider.VerifiedArchiveSha256, provider.SourceSha256);
    }

    [Fact]
    public void CnsProfileRejectsConflictingAndUnmappedData()
    {
        using var first = new StringReader("1-2121\t4E00\n");
        using var conflicting = new StringReader("1-2121\t4E01\n");

        Assert.Throws<InvalidDataException>(() => Cns11643EucTwMappingProvider.Load(
            [first, conflicting],
            10));

        using var valid = new StringReader("1-2121\t4E00\n");
        Cns11643EucTwMappingProvider provider = Cns11643EucTwMappingProvider.Load([valid], 10);
        Assert.Throws<DecoderFallbackException>(() => provider.Decode(new byte[] { 0xA1, 0xA2 }));
    }
}
