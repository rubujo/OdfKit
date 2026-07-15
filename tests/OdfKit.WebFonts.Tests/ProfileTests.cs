using System.Text;
using OdfKit.WebFonts.Profiles;

namespace OdfKit.WebFonts.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void JsonProfile_DecodesMultibyteAndPrivateMappings()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "profileId": "international-agency-v1",
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
    }
}
