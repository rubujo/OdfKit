using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class Cff2CharStringVerifierTests
{
    [Fact]
    public void Verify_AcceptsBlendAndVariationIndex()
    {
        byte[] charString = [140, 15, 139, 140, 141, 140, 16, 22];

        Cff2CharStringVerifier.Verify(
            charString,
            [],
            [],
            [1, 2],
            defaultVariationIndex: 0,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Verify_AcceptsEmptyProgram()
    {
        Cff2CharStringVerifier.Verify(
            Array.Empty<byte>(),
            [],
            [],
            [1],
            defaultVariationIndex: 0,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Verify_RejectsHintMaskWithoutStemHints()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                new byte[] { 19 },
                [],
                [],
                [1],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("hintmask-stem", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_AcceptsNonVariableContextWithoutVariationStore()
    {
        Cff2CharStringVerifier.Verify(
            Array.Empty<byte>(),
            [],
            [],
            [],
            defaultVariationIndex: 0,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Verify_RejectsBlendWithoutVariationStore()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                new byte[] { 139, 16 },
                [],
                [],
                [],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("blend-without-vstore", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((byte)11)]
    [InlineData((byte)14)]
    public void Verify_RejectsRemovedType2Operators(byte operation)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                new byte[] { operation },
                [],
                [],
                [1],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("removed-operator", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsBlendWithInsufficientOperands()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                new byte[] { 139, 140, 16 },
                [],
                [],
                [2],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("blend-stack", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsRecursiveSubroutineBeyondLimit()
    {
        byte[] charString = [32, 10];
        ReadOnlyMemory<byte>[] localSubroutines = [new byte[] { 32, 10 }];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                charString,
                [],
                localSubroutines,
                [1],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("depth", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_RejectsOperandStackOverflow()
    {
        byte[] charString = Enumerable.Repeat((byte)139, 514).ToArray();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2CharStringVerifier.Verify(
                charString,
                [],
                [],
                [1],
                defaultVariationIndex: 0,
                TestContext.Current.CancellationToken));

        Assert.Contains("stack", exception.Message, StringComparison.Ordinal);
    }
}
