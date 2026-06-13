using System.Diagnostics;

namespace OdfKit.Compliance;

/// <summary>
/// 提供呼叫外部 ODF 驗證器的輔助方法。
/// </summary>
public static class OdfExternalValidator
{
    /// <summary>
    /// 取得 ODF Validator JAR 路徑的環境變數名稱。
    /// </summary>
    public const string OdfValidatorJarEnvironmentVariable = "ODFKIT_ODFVALIDATOR_JAR";

    /// <summary>
    /// 使用 ODF Toolkit 的 ODF Validator JAR 驗證指定文件。
    /// </summary>
    /// <param name="filePath">要驗證的 ODF 文件路徑。</param>
    /// <param name="jarPath">ODF Validator JAR 路徑。若未提供，會讀取 <c>ODFKIT_ODFVALIDATOR_JAR</c>。</param>
    /// <param name="javaPath">Java 執行檔路徑。預設使用 <c>java</c>。</param>
    /// <param name="timeoutMilliseconds">外部程序逾時毫秒數。</param>
    /// <returns>外部驗證器執行結果。</returns>
    /// <exception cref="ArgumentException">當文件路徑或 JAR 路徑未提供時擲出。</exception>
    /// <exception cref="FileNotFoundException">當文件或 JAR 不存在時擲出。</exception>
    /// <exception cref="TimeoutException">當外部程序逾時時擲出。</exception>
    public static OdfExternalValidatorResult ValidateWithOdfValidator(
        string filePath,
        string? jarPath = null,
        string? javaPath = null,
        int timeoutMilliseconds = 30000)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路徑不可空白。", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到要驗證的文件。", filePath);
        }

        string? resolvedJarPath = string.IsNullOrWhiteSpace(jarPath)
            ? Environment.GetEnvironmentVariable(OdfValidatorJarEnvironmentVariable)
            : jarPath;
        if (string.IsNullOrWhiteSpace(resolvedJarPath))
        {
            throw new ArgumentException("未提供 ODF Validator JAR 路徑。", nameof(jarPath));
        }

        if (!File.Exists(resolvedJarPath))
        {
            throw new FileNotFoundException("找不到 ODF Validator JAR。", resolvedJarPath);
        }

        string resolvedJavaPath = string.IsNullOrWhiteSpace(javaPath) ? "java" : javaPath!;
        return RunProcess(
            resolvedJavaPath,
            ["-jar", resolvedJarPath!, filePath],
            timeoutMilliseconds);
    }

    /// <summary>
    /// 執行可選外部驗證命令並以結束碼判定 valid / invalid。
    /// </summary>
    /// <param name="commandPath">外部命令路徑。</param>
    /// <param name="filePath">要驗證的 ODF 文件路徑。</param>
    /// <param name="timeoutMilliseconds">外部程序逾時毫秒數。</param>
    /// <returns>外部驗證器執行結果。</returns>
    public static OdfExternalValidatorResult ValidateWithCommand(
        string commandPath,
        string filePath,
        int timeoutMilliseconds = 30000)
    {
        if (string.IsNullOrWhiteSpace(commandPath))
        {
            throw new ArgumentException("外部命令路徑不可空白。", nameof(commandPath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路徑不可空白。", nameof(filePath));
        }

        if (!File.Exists(commandPath))
        {
            throw new FileNotFoundException("找不到外部命令。", commandPath);
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到要驗證的文件。", filePath);
        }

        return RunProcess(commandPath, [filePath], timeoutMilliseconds);
    }

    private static OdfExternalValidatorResult RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds)
    {
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "逾時必須大於 0。");
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(" ", arguments.Select(EscapeArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException("外部 ODF 驗證器逾時。");
        }

        string standardOutput = standardOutputTask.GetAwaiter().GetResult();
        string standardError = standardErrorTask.GetAwaiter().GetResult();
        return new OdfExternalValidatorResult(
            process.ExitCode,
            standardOutput,
            standardError);
    }

    private static string EscapeArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
        {
            return argument;
        }

        return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

/// <summary>
/// 表示外部 ODF 驗證器執行結果。
/// </summary>
/// <param name="exitCode">程序結束碼。</param>
/// <param name="standardOutput">標準輸出內容。</param>
/// <param name="standardError">標準錯誤內容。</param>
public sealed class OdfExternalValidatorResult(
    int exitCode,
    string standardOutput,
    string standardError)
{
    /// <summary>
    /// 取得程序結束碼。
    /// </summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>
    /// 取得標準輸出內容。
    /// </summary>
    public string StandardOutput { get; } = standardOutput;

    /// <summary>
    /// 取得標準錯誤內容。
    /// </summary>
    public string StandardError { get; } = standardError;

    /// <summary>
    /// 取得外部驗證器是否將文件分類為有效。
    /// </summary>
    public bool IsValid => ExitCode == 0;
}
