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
    /// 驗證當註冊替代對照時傳入 Null 或空字串，應正確拋出 ArgumentNullException 異常。
    /// </summary>
    [Fact]
    public void RegisterFallback_NullOrEmptyArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback(null!, "Font"));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback("Font", null!));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback(string.Empty, "Font"));
        Assert.Throws<ArgumentNullException>(() => OdfFontResolver.RegisterFallback("Font", string.Empty));
    }
}
