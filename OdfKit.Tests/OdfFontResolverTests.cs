using System;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfFontResolver 的字型替代對照與對應之單元測試。
/// </summary>
public class OdfFontResolverTests
{
    /// <summary>
    /// 驗證字型替換對照規則的註冊與解析對照是否正確。
    /// </summary>
    [Fact]
    public void RegisterFallback_And_MapFont_ResolvesCorrectly()
    {
        // 1. 註冊替代對照規則
        string target = "Microsoft YaHei";
        string replacement = "Noto Sans CJK TC";
        OdfFontResolver.RegisterFallback(target, replacement);

        // 2. 驗證已註冊字型的替代對照
        string mapped = OdfFontResolver.MapFont(target);
        Assert.Equal(replacement, mapped);

        // 3. 驗證未註冊字型對照應回傳原字型名稱
        string unregistered = "Unregistered Font Family";
        string original = OdfFontResolver.MapFont(unregistered);
        Assert.Equal(unregistered, original);

        // 4. 驗證空值或 Null 傳回原字串
        Assert.Equal(string.Empty, OdfFontResolver.MapFont(string.Empty));
        Assert.Null(OdfFontResolver.MapFont(null!));
    }

    /// <summary>
    /// 驗證當註冊替代對照時傳入 Null 或空字串，應正確拋出 ArgumentNullException 例外。
    /// </summary>
    [Fact]
    public void RegisterFallback_NullOrEmptyArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback(null!, "Font"));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback("Font", null!));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback(string.Empty, "Font"));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback("Font", string.Empty));
    }

    /// <summary>
    /// 驗證內建跨平台字型對照表會為常見 Office 與臺灣 CJK 字型提供替代候選。
    /// </summary>
    [Fact]
    public void BuiltInFallbackCandidatesCoverOfficeAndTaiwanCjkFonts()
    {
        IReadOnlyList<string> calibri = OdfFontResolver.GetFontFallbackCandidates("Calibri");
        Assert.Equal("Calibri", calibri[0]);
        Assert.Contains("Carlito", calibri);
        Assert.Contains("DejaVu Sans", calibri);

        IReadOnlyList<string> cambria = OdfFontResolver.GetFontFallbackCandidates("Cambria");
        Assert.Equal("Cambria", cambria[0]);
        Assert.Contains("Caladea", cambria);
        Assert.Contains("DejaVu Serif", cambria);

        IReadOnlyList<string> pmingliu = OdfFontResolver.GetFontFallbackCandidates("新細明體");
        Assert.Equal("新細明體", pmingliu[0]);
        Assert.Contains("Noto Serif CJK TC", pmingliu);
        Assert.Contains("Source Han Serif TC", pmingliu);
    }

    /// <summary>
    /// 驗證使用者註冊的替代字型會排在內建候選之前，並由可用性探針選出第一個可使用候選。
    /// </summary>
    [Fact]
    public void ResolveFontFallbackUsesRegisteredFallbackBeforeBuiltInCandidates()
    {
        OdfFontResolver.RegisterFallback("Calibri", "Company Sans");

        IReadOnlyList<string> candidates = OdfFontResolver.GetFontFallbackCandidates("Calibri");
        Assert.Equal("Calibri", candidates[0]);
        Assert.Equal("Company Sans", candidates[1]);

        string? resolved = OdfFontResolver.ResolveFontFallback(
            "Calibri",
            name => name == "Carlito");

        Assert.Equal("Carlito", resolved);

        string? registeredResolved = OdfFontResolver.ResolveFontFallback(
            "Calibri",
            name => name == "Company Sans" || name == "Carlito");

        Assert.Equal("Company Sans", registeredResolved);
    }

    /// <summary>
    /// 驗證空白字型沒有候選，而 Null 可用性探針會明確拋出例外。
    /// </summary>
    [Fact]
    public void ResolveFontFallbackHandlesEmptyInputAndNullProbe()
    {
        Assert.Empty(OdfFontResolver.GetFontFallbackCandidates(string.Empty));
        Assert.Null(OdfFontResolver.ResolveFontFallback(string.Empty, _ => true));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.ResolveFontFallback("Calibri", null!));
    }
}
