using OdfKit.WebFonts;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontTextSequenceTests
{
    [Fact]
    public void Create_PreservesIvsAndSupplementaryPuaOrder()
    {
        string text = "邉\U000E0110\U000F0000";

        WebFontTextSequence sequence = WebFontTextSequence.Create(text);

        Assert.Equal(text, sequence.Text);
        Assert.Equal([0x9089, 0xE0110, 0xF0000], sequence.UnicodeScalars);
    }

    [Fact]
    public void Create_RejectsUnpairedSurrogate()
    {
        Assert.Throws<ArgumentException>(() => WebFontTextSequence.Create(new string((char)0xD800, 1)));
        Assert.Throws<ArgumentException>(() => WebFontTextSequence.Create(new string((char)0xDC00, 1)));
    }
}
