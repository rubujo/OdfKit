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

    /// <summary>
    /// 驗證同步包裝方法 <see cref="OdfExternalValidator.ValidateWithCommand"/> 能成功委派至非同步管線並回傳正確結果，
    /// 鎖定 <see cref="OdfExternalValidatorAsyncTests.ValidateWithCommandAsync_DefaultToken_CompletesSuccessfully"/>
    /// 已涵蓋的非同步路徑之外，同步呼叫端（無 <c>async</c>／<c>await</c>）的呼叫慣例仍正確運作。
    /// </summary>
    [Fact]
    public void ValidateWithCommand_SyncWrapper_DelegatesToAsyncAndCompletesSuccessfully()
    {
        string commandPath = CreateNoOpCommand();
        string filePath = CreateTempOdfFile();
        try
        {
            OdfExternalValidatorResult result = OdfExternalValidator.ValidateWithCommand(commandPath, filePath);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.IsValid);
        }
        finally
        {
            TryDelete(commandPath);
            TryDelete(filePath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfExternalValidator.ValidateWithOdfValidator"/> 同步包裝方法在未提供 JAR 路徑
    /// 且未設定 <c>ODFKIT_ODFVALIDATOR_JAR</c> 環境變數時，正確擲出 <see cref="ArgumentException"/>
    /// （本機未安裝 ODF Validator JAR，故僅能驗證參數防禦邏輯，無法驗證真實 JAR 執行結果）。
    /// </summary>
    [Fact]
    public void ValidateWithOdfValidator_MissingJarPath_ThrowsArgumentException()
    {
        string? originalEnv = Environment.GetEnvironmentVariable(OdfExternalValidator.OdfValidatorJarEnvironmentVariable);
        string filePath = CreateTempOdfFile();
        try
        {
            Environment.SetEnvironmentVariable(OdfExternalValidator.OdfValidatorJarEnvironmentVariable, null);

            Assert.Throws<ArgumentException>(() => OdfExternalValidator.ValidateWithOdfValidator(filePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable(OdfExternalValidator.OdfValidatorJarEnvironmentVariable, originalEnv);
            TryDelete(filePath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfExternalValidator.ValidateWithOdfValidator"/> 同步包裝方法在提供的 JAR 路徑
    /// 實際不存在時，正確擲出 <see cref="FileNotFoundException"/>。
    /// </summary>
    [Fact]
    public void ValidateWithOdfValidator_NonexistentJarPath_ThrowsFileNotFoundException()
    {
        string filePath = CreateTempOdfFile();
        string nonexistentJarPath = Path.Combine(Path.GetTempPath(), "odfkit_nonexistent_" + Guid.NewGuid().ToString("N") + ".jar");
        try
        {
            Assert.Throws<FileNotFoundException>(() => OdfExternalValidator.ValidateWithOdfValidator(filePath, nonexistentJarPath));
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfExternalValidator.ValidateWithOdfValidatorAsync"/> 對不存在的待驗證文件路徑
    /// 正確擲出 <see cref="FileNotFoundException"/>（在檢查 JAR 路徑之前即先驗證文件是否存在）。
    /// </summary>
    [Fact]
    public async Task ValidateWithOdfValidatorAsync_NonexistentDocumentPath_ThrowsFileNotFoundException()
    {
        string nonexistentDocumentPath = Path.Combine(Path.GetTempPath(), "odfkit_nonexistent_" + Guid.NewGuid().ToString("N") + ".odt");

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await OdfExternalValidator.ValidateWithOdfValidatorAsync(nonexistentDocumentPath, jarPath: "dummy.jar");
        });
    }
}
