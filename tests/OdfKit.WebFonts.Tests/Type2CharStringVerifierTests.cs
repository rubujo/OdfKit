using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class Type2CharStringVerifierTests
{
    [Fact]
    public void VerifyAcceptsCalculatedLocalSubroutineIndex()
    {
        byte[] charString = [251, 0, 140, 12, 10, 10, 14];
        ReadOnlyMemory<byte>[] localSubroutines = [new byte[] { 139, 139, 21, 11 }];

        Type2CharStringVerifier.Verify(
            charString,
            [],
            localSubroutines,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void VerifyReturnsSeacComponentsReachedThroughLocalSubroutine()
    {
        byte[] charString = [32, 10, 14];
        ReadOnlyMemory<byte>[] localSubroutines = [new byte[] { 139, 139, 204, 247, 86, 14 }];

        Type2SeacComponents? components = Type2CharStringVerifier.Verify(
            charString,
            [],
            localSubroutines,
            TestContext.Current.CancellationToken);

        Assert.True(components.HasValue);
        Assert.Equal(65, components.Value.BaseCode);
        Assert.Equal(194, components.Value.AccentCode);
    }

    [Theory]
    [InlineData(new byte[] { 139, 139, 255, 0, 65, 128, 0, 247, 86, 14 }, "seac-bchar")]
    [InlineData(new byte[] { 139, 139, 204, 247, 148, 14 }, "seac-achar")]
    public void VerifyRejectsInvalidSeacComponentCodes(byte[] charString, string detail)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Type2CharStringVerifier.Verify(
                charString,
                [],
                [],
                TestContext.Current.CancellationToken));

        Assert.Contains(detail, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRejectsRecursiveSubroutineBeyondLimit()
    {
        byte[] charString = [32, 10, 14];
        ReadOnlyMemory<byte>[] localSubroutines = [new byte[] { 32, 10, 11 }];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Type2CharStringVerifier.Verify(
                charString,
                [],
                localSubroutines,
                TestContext.Current.CancellationToken));

        Assert.Contains("depth", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRejectsOperandStackOverflow()
    {
        byte[] charString = Enumerable.Repeat((byte)139, 49).Append((byte)14).ToArray();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Type2CharStringVerifier.Verify(
                charString,
                [],
                [],
                TestContext.Current.CancellationToken));

        Assert.Contains("stack", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRejectsTruncatedHintMask()
    {
        byte[] charString = [139, 139, 1, 19];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Type2CharStringVerifier.Verify(
                charString,
                [],
                [],
                TestContext.Current.CancellationToken));

        Assert.Contains("hintmask", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyAcceptsZeroLengthHintMaskAndClampedIndex()
    {
        byte[] charString =
        [139, 140, 251, 0, 12, 29, 12, 18, 12, 18, 12, 18, 19, 14];

        Type2CharStringVerifier.Verify(
            charString,
            [],
            [],
            TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(new byte[] { 140, 139, 12, 12, 14 }, "div-zero")]
    [InlineData(new byte[] { 138, 12, 26, 14 }, "sqrt-negative")]
    public void VerifyRejectsKnownInvalidArithmetic(byte[] charString, string detail)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Type2CharStringVerifier.Verify(
                charString,
                [],
                [],
                TestContext.Current.CancellationToken));

        Assert.Contains(detail, exception.Message, StringComparison.Ordinal);
    }
}
