using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class ManagedOpenTypeWebFontVerifierTests
{
    [Fact]
    public void VerifyRejectsInputBeyondIndependentLimit()
    {
        using var stream = new MemoryStream(new byte[5]);
        var options = new ManagedOpenTypeWebFontVerificationOptions
        {
            MaximumInputBytes = 4,
            MaximumExpandedBytes = 64,
            MaximumTableCount = 16
        };

        Assert.Throws<InvalidDataException>(() => ManagedOpenTypeWebFontVerifier.Verify(
            stream,
            WebFontFormat.TrueType,
            options,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(257)]
    public void VerifyRejectsUnsafeTableCountLimit(int maximumTableCount)
    {
        using var stream = new MemoryStream([0, 1, 0, 0]);
        var options = new ManagedOpenTypeWebFontVerificationOptions
        {
            MaximumInputBytes = 64,
            MaximumExpandedBytes = 64,
            MaximumTableCount = maximumTableCount
        };

        Assert.Throws<ArgumentException>(() => ManagedOpenTypeWebFontVerifier.Verify(
            stream,
            WebFontFormat.TrueType,
            options,
            TestContext.Current.CancellationToken));
    }
}
