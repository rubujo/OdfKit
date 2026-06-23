using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Text;
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
            await OdfExternalValidator.ValidateWithOdfValidatorAsync(nonexistentDocumentPath, jarPath: "dummy.jar", cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    /// <summary>
    /// 真機驗證：當設定 <c>ODFKIT_ODFVALIDATOR_JAR</c> 指向真實的 ODF Toolkit Validator JAR 時，
    /// <see cref="OdfExternalValidator.ValidateWithOdfValidator"/> 應正確回報合規文件為有效、結構毀損
    /// 文件為無效（含實際的錯誤訊息內容），驗證真實 JAR 呼叫路徑，而非僅停留在參數防護邏輯。
    /// </summary>
    [Fact]
    public void ValidateWithOdfValidator_RealJar_DetectsValidAndInvalidDocuments()
    {
        string? jarPath = Environment.GetEnvironmentVariable(OdfExternalValidator.OdfValidatorJarEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(jarPath) || !File.Exists(jarPath))
        {
            Assert.Skip("未設定 ODFKIT_ODFVALIDATOR_JAR 或檔案不存在，略過真實 ODF Validator JAR 驗收。");
        }

        string validPath = CreateValidOdtFile();
        string invalidPath = CreateStructurallyInvalidOdtFile(validPath);
        try
        {
            OdfExternalValidatorResult validResult = OdfExternalValidator.ValidateWithOdfValidator(validPath, jarPath);
            Assert.Equal(0, validResult.ExitCode);
            Assert.True(validResult.IsValid);

            OdfExternalValidatorResult invalidResult = OdfExternalValidator.ValidateWithOdfValidator(invalidPath, jarPath);
            Assert.NotEqual(0, invalidResult.ExitCode);
            Assert.False(invalidResult.IsValid);
            Assert.Contains("office:text", invalidResult.StandardOutput + invalidResult.StandardError);
        }
        finally
        {
            TryDelete(validPath);
            TryDelete(invalidPath);
        }
    }

    private static string CreateValidOdtFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "odfkit_validator_valid_" + Guid.NewGuid().ToString("N") + ".odt");
        using var document = TextDocument.Create();
        document.Body.Headings.Add("ODF Validator 真機驗收標題", 1);
        document.Body.Paragraphs.Add("這是用來驗證真實 ODF Toolkit Validator JAR 的合規文件內容。");
        document.Save(path);
        return path;
    }

    private static string CreateStructurallyInvalidOdtFile(string validOdtPath)
    {
        string invalidPath = Path.Combine(Path.GetTempPath(), "odfkit_validator_invalid_" + Guid.NewGuid().ToString("N") + ".odt");

        var entries = new Dictionary<string, byte[]>();
        using (var sourceArchive = ZipFile.OpenRead(validOdtPath))
        {
            foreach (var entry in sourceArchive.Entries)
            {
                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                entries[entry.FullName] = buffer.ToArray();
            }
        }

        string contentXml = Encoding.UTF8.GetString(entries["content.xml"]);
        contentXml = contentXml.Replace("</office:text>", "</office:text><office:text>broken-nested-element");
        entries["content.xml"] = Encoding.UTF8.GetBytes(contentXml);

        using var destination = new FileStream(invalidPath, FileMode.Create, FileAccess.Write);
        using var destinationArchive = new ZipArchive(destination, ZipArchiveMode.Create);
        foreach (var pair in entries)
        {
            var compression = pair.Key == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
            var newEntry = destinationArchive.CreateEntry(pair.Key, compression);
            using var newEntryStream = newEntry.Open();
            newEntryStream.Write(pair.Value, 0, pair.Value.Length);
        }

        return invalidPath;
    }
}
