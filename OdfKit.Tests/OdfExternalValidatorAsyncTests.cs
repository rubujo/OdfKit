using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證外部 ODF 驗證器非同步管線的 CancellationToken 協作取消行為。
/// </summary>
public class OdfExternalValidatorAsyncTests
{
    /// <summary>
    /// 預先取消的語彙應使 ValidateWithCommandAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task ValidateWithCommandAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        string commandPath = CreateNoOpCommand();
        string filePath = CreateTempOdfFile();
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await OdfExternalValidator.ValidateWithCommandAsync(commandPath, filePath, cancellationToken: cts.Token);
            });
        }
        finally
        {
            TryDelete(commandPath);
            TryDelete(filePath);
        }
    }

    /// <summary>
    /// 未取消時 ValidateWithCommandAsync 應成功執行並回傳有效結果。
    /// </summary>
    [Fact]
    public async Task ValidateWithCommandAsync_DefaultToken_CompletesSuccessfully()
    {
        string commandPath = CreateNoOpCommand();
        string filePath = CreateTempOdfFile();
        try
        {
            OdfExternalValidatorResult result = await OdfExternalValidator.ValidateWithCommandAsync(
                commandPath,
                filePath,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.IsValid);
        }
        finally
        {
            TryDelete(commandPath);
            TryDelete(filePath);
        }
    }

    private static string CreateTempOdfFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".odt");
        File.WriteAllText(path, "placeholder");
        return path;
    }

    private static string CreateNoOpCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            string path = Path.Combine(Path.GetTempPath(), "odfkit_noop_" + Guid.NewGuid().ToString("N") + ".cmd");
            File.WriteAllText(path, "@echo off\r\nexit /b 0\r\n");
            return path;
        }

        return "/usr/bin/true";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
