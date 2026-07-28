using System;
using System.IO;
using OdfKit.Benchmarks;
using Xunit;

namespace OdfKit.Tests;

public sealed class BenchmarkProcessGuardTests
{
    [Fact]
    public void EnsureUniqueArtifactsPathAddsGeneratedPath()
    {
        string[] args = ["--filter", "*TextAutoFitBenchmarks*"];

        string[] result = BenchmarkProcessGuard.EnsureUniqueArtifactsPath(args, () => "generated-artifacts");

        Assert.Equal(["--filter", "*TextAutoFitBenchmarks*", "--artifacts", "generated-artifacts"], result);
        Assert.NotSame(args, result);
    }

    [Theory]
    [InlineData("--artifacts")]
    [InlineData("--artifacts=custom")]
    [InlineData("--ARTIFACTS=custom")]
    public void EnsureUniqueArtifactsPathPreservesExplicitPath(string artifactsArgument)
    {
        string[] args = [artifactsArgument, "custom"];

        string[] result = BenchmarkProcessGuard.EnsureUniqueArtifactsPath(
            args,
            () => throw new InvalidOperationException("不應建立預設路徑。"));

        Assert.Same(args, result);
    }

    [Fact]
    public void RunReturnsFailureAndWritesException()
    {
        using var errorWriter = new StringWriter();

        int exitCode = BenchmarkProcessGuard.Run(
            () => throw new InvalidOperationException("expected failure"),
            errorWriter);

        Assert.Equal(1, exitCode);
        Assert.Contains("OdfKit.Benchmarks 執行失敗", errorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("expected failure", errorWriter.ToString(), StringComparison.Ordinal);
    }
}
