using System.Diagnostics;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

/// <summary>
/// CharString 直譯器限制遞迴深度（10），但深度上限不限制每層的呼叫廣度。
/// 巢狀 subroutine 因而可產生 breadth^depth 的展開量：約 600 位元組的 subroutine
/// 資料即足以造成實質無限的執行時間，且會繞過 worker 的 JobTimeout——取消權杖
/// 只在逐字圖迴圈檢查，單一字圖會卡死在 Verify 內部。因此另設總操作預算。
/// </summary>
public sealed class CharStringBudgetTests
{
    /// <summary>
    /// 指數展開必須被操作預算攔截，而不是讓它跑完。
    /// </summary>
    [Fact]
    public void NestedSubroutineExpansionIsRejectedByOperationBudget()
    {
        // 深度 10、每層 20 次呼叫 ≈ 1.0e13 次展開；修正前此輸入不會終止。
        (byte[] charString, byte[][] subroutines) = CreateNestedProgram(depth: 10, breadth: 20);
        ReadOnlyMemory<byte>[] locals = subroutines
            .Select(value => new ReadOnlyMemory<byte>(value))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => Type2CharStringVerifier.Verify(charString, [], locals));
        stopwatch.Stop();

        Assert.Contains("operation-budget", exception.Message, StringComparison.Ordinal);

        // 預算的目的就是把最壞情況壓在可接受的時間內；若耗時失控代表預算失效。
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"操作預算未能及時攔截，耗時 {stopwatch.Elapsed.TotalSeconds:F1} 秒。");
    }

    /// <summary>
    /// 預算不得誤傷合法字型：正常規模的巢狀 subroutine 必須照常通過。
    /// </summary>
    [Fact]
    public void ModestNestedSubroutineExpansionStillVerifies()
    {
        (byte[] charString, byte[][] subroutines) = CreateNestedProgram(depth: 5, breadth: 6);
        ReadOnlyMemory<byte>[] locals = subroutines
            .Select(value => new ReadOnlyMemory<byte>(value))
            .ToArray();

        Type2SeacComponents? result = Type2CharStringVerifier.Verify(charString, [], locals);

        Assert.Null(result);
    }

    /// <summary>
    /// 取消權杖必須能中斷直譯器；先前權杖到不了這一層。
    /// </summary>
    [Fact]
    public void PreCancelledTokenStopsVerification()
    {
        (byte[] charString, byte[][] subroutines) = CreateNestedProgram(depth: 10, breadth: 20);
        ReadOnlyMemory<byte>[] locals = subroutines
            .Select(value => new ReadOnlyMemory<byte>(value))
            .ToArray();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => Type2CharStringVerifier.Verify(charString, [], locals, cancellation.Token));
    }

    /// <summary>
    /// 建立 depth 層、每層 breadth 次呼叫的巢狀 local subroutine 程式。
    /// </summary>
    /// <remarks>
    /// subroutine 數量少於 1,240，bias 為 107；索引 i 的運算元為 i - 107，
    /// 落在單位元組整數範圍內，編碼為 i + 32。
    /// </remarks>
    private static (byte[] CharString, byte[][] Subroutines) CreateNestedProgram(int depth, int breadth)
    {
        var subroutines = new byte[depth][];
        for (int level = 0; level < depth; level++)
        {
            var body = new List<byte>();
            if (level + 1 < depth)
            {
                for (int call = 0; call < breadth; call++)
                {
                    body.Add((byte)(level + 1 + 32));
                    body.Add(10);
                }
            }

            body.Add(11);
            subroutines[level] = body.ToArray();
        }

        var charString = new List<byte>();
        for (int call = 0; call < breadth; call++)
        {
            charString.Add(32);
            charString.Add(10);
        }

        charString.Add(14);
        return (charString.ToArray(), subroutines);
    }
}
