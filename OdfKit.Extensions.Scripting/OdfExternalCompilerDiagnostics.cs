using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Text;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Identifies an external compiler used for non-executing syntax diagnostics.
/// 識別用於非執行式語法診斷的外部編譯器。
/// </summary>
public enum OdfScriptCompilerBackend
{
    /// <summary>
    /// The Python abstract-syntax-tree compiler.
    /// Python 抽象語法樹編譯器。
    /// </summary>
    PythonAst,

    /// <summary>
    /// The LibreOffice Basic compiler hosted by an isolated office process.
    /// 由隔離辦公軟體處理程序承載的 LibreOffice Basic 編譯器。
    /// </summary>
    LibreOfficeBasic
}

/// <summary>
/// Identifies the outcome of an external compiler diagnostic run.
/// 識別外部編譯器診斷執行結果。
/// </summary>
public enum OdfScriptCompilationStatus
{
    /// <summary>
    /// The compiler accepted the source.
    /// 編譯器接受原始碼。
    /// </summary>
    Valid,

    /// <summary>
    /// The backend completed only a safe partial probe and cannot prove compiler acceptance.
    /// 後端僅完成安全的部分探測，無法證明編譯器接受原始碼。
    /// </summary>
    Indeterminate,

    /// <summary>
    /// The compiler rejected the source.
    /// 編譯器拒絕原始碼。
    /// </summary>
    Invalid,

    /// <summary>
    /// The requested compiler is unavailable.
    /// 要求的編譯器無法使用。
    /// </summary>
    Unavailable,

    /// <summary>
    /// The compiler worker timed out.
    /// 編譯器 worker 逾時。
    /// </summary>
    TimedOut,

    /// <summary>
    /// The compiler worker failed without a syntax decision.
    /// 編譯器 worker 失敗，且未產生語法判定。
    /// </summary>
    WorkerFailed
}

/// <summary>
/// Configures external compiler worker paths and resource bounds.
/// 設定外部編譯器 worker 路徑與資源界線。
/// </summary>
public sealed class OdfScriptCompilerOptions
{
    /// <summary>
    /// Gets or sets the Python executable used for AST compilation.
    /// 取得或設定用於 AST 編譯的 Python 執行檔。
    /// </summary>
    public string? PythonExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the LibreOffice <c>soffice</c> executable.
    /// 取得或設定 LibreOffice <c>soffice</c> 執行檔。
    /// </summary>
    public string? LibreOfficeExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the Python executable bundled with LibreOffice for UNO access.
    /// 取得或設定 LibreOffice 隨附且用於 UNO 存取的 Python 執行檔。
    /// </summary>
    public string? LibreOfficePythonExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the maximum worker duration.
    /// 取得或設定 worker 的最長執行時間。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Describes a diagnostic emitted by a real external compiler.
/// 描述真實外部編譯器產生的診斷。
/// </summary>
public sealed class OdfScriptCompilerDiagnostic
{
    internal OdfScriptCompilerDiagnostic(
        string code,
        int line,
        int column,
        string? detail)
    {
        Code = code;
        Line = line;
        Column = column;
        Detail = detail;
    }

    /// <summary>
    /// Gets the stable OdfKit diagnostic code.
    /// 取得穩定的 OdfKit 診斷代碼。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the one-based line number, or zero when unavailable.
    /// 取得從一開始的行號；無法取得時為零。
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the one-based column number, or zero when unavailable.
    /// 取得從一開始的欄號；無法取得時為零。
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets bounded compiler-provided detail.
    /// 取得有界的編譯器詳細資訊。
    /// </summary>
    public string? Detail { get; }
}

/// <summary>
/// Reports an external compiler decision separately from structural diagnostics.
/// 將外部編譯器判定與結構式診斷分開回報。
/// </summary>
public sealed class OdfScriptCompilationResult
{
    internal OdfScriptCompilationResult(
        OdfScriptCompilerBackend backend,
        OdfScriptCompilationStatus status,
        IReadOnlyList<OdfScriptCompilerDiagnostic> diagnostics)
    {
        Backend = backend;
        Status = status;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the compiler backend.
    /// 取得編譯器後端。
    /// </summary>
    public OdfScriptCompilerBackend Backend { get; }

    /// <summary>
    /// Gets the compiler outcome.
    /// 取得編譯器結果。
    /// </summary>
    public OdfScriptCompilationStatus Status { get; }

    /// <summary>
    /// Gets compiler diagnostics.
    /// 取得編譯器診斷。
    /// </summary>
    public IReadOnlyList<OdfScriptCompilerDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Runs real language compilers in bounded child processes without invoking user routines.
/// 在有界子處理程序中執行真實語言編譯器，且不呼叫使用者常式。
/// </summary>
public static class OdfExternalScriptCompiler
{
    private const int MaximumDetailLength = 4096;

    /// <summary>
    /// Diagnoses source with a real external compiler worker.
    /// 使用真實外部編譯器 worker 診斷原始碼。
    /// </summary>
    /// <param name="source">The source text. / 原始碼文字。</param>
    /// <param name="backend">The compiler backend. / 編譯器後端。</param>
    /// <param name="options">Worker paths and limits. / worker 路徑與界線。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The compiler decision. / 編譯器判定。</returns>
    public static Task<OdfScriptCompilationResult> DiagnoseAsync(
        string source,
        OdfScriptCompilerBackend backend,
        OdfScriptCompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));
        }
        if (options is null)
        {
            throw new ArgumentNullException(
                nameof(options),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(options)));
        }
        if (!Enum.IsDefined(typeof(OdfScriptCompilerBackend), backend) ||
            options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(options)));
        }

        return Task.Run(
            () => backend == OdfScriptCompilerBackend.PythonAst
                ? DiagnosePython(source, options, cancellationToken)
                : DiagnoseLibreOfficeBasic(source, options, cancellationToken),
            cancellationToken);
    }

    private static OdfScriptCompilationResult DiagnosePython(
        string source,
        OdfScriptCompilerOptions options,
        CancellationToken cancellationToken)
    {
        string? executable = ResolveExecutable(options.PythonExecutablePath);
        if (executable is null)
            return Result(OdfScriptCompilerBackend.PythonAst, OdfScriptCompilationStatus.Unavailable);

        string root = CreateWorkerDirectory();
        try
        {
            string sourcePath = Path.Combine(root, "source.py");
            string helperPath = Path.Combine(root, "compile.py");
            File.WriteAllText(sourcePath, source, new UTF8Encoding(false));
            File.WriteAllText(helperPath, PythonCompilerWorker, new UTF8Encoding(false));
            ProcessResult process = RunProcess(
                executable,
                $"-I -S {QuoteArgument(helperPath)} {QuoteArgument(sourcePath)}",
                options.Timeout,
                cancellationToken);
            return ParseWorkerResult(OdfScriptCompilerBackend.PythonAst, process);
        }
        finally
        {
            DeleteWorkerDirectory(root);
        }
    }

    private static OdfScriptCompilationResult DiagnoseLibreOfficeBasic(
        string source,
        OdfScriptCompilerOptions options,
        CancellationToken cancellationToken)
    {
        string? soffice = ResolveExecutable(options.LibreOfficeExecutablePath);
        if (soffice is null)
            return Result(OdfScriptCompilerBackend.LibreOfficeBasic, OdfScriptCompilationStatus.Unavailable);

        string? python = ResolveLibreOfficePython(soffice, options.LibreOfficePythonExecutablePath);
        if (python is null)
            return Result(OdfScriptCompilerBackend.LibreOfficeBasic, OdfScriptCompilationStatus.Unavailable);

        string root = CreateWorkerDirectory();
        Process? office = null;
        try
        {
            string profile = Path.Combine(root, "profile");
            string profileUser = Path.Combine(profile, "user");
            Directory.CreateDirectory(profileUser);
            File.WriteAllText(
                Path.Combine(profileUser, "registrymodifications.xcu"),
                MacroSecurityProfile,
                new UTF8Encoding(false));

            string module = PrepareBasicCompileModule(source, out string entryPoint);
            string documentPath = Path.Combine(root, "compile.odt");
            using (TextDocument document = TextDocument.Create())
            {
                document.Scripting().AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", module);
                document.Save(documentPath);
            }

            string bridgePath = Path.Combine(root, "compile-basic.py");
            File.WriteAllText(bridgePath, LibreOfficeBasicCompilerWorker, new UTF8Encoding(false));
            int port = ReserveTcpPort();
            office = StartLibreOffice(soffice, profile, port);
            string uri = $"vnd.sun.star.script:Standard.Module1.{entryPoint}?language=Basic&location=document";
            ProcessResult worker = RunProcess(
                python,
                $"{QuoteArgument(bridgePath)} {port.ToString(CultureInfo.InvariantCulture)} " +
                $"{QuoteArgument(documentPath)} {QuoteArgument(uri)}",
                options.Timeout,
                cancellationToken);
            OdfScriptCompilationResult result = ParseWorkerResult(OdfScriptCompilerBackend.LibreOfficeBasic, worker);
            if (result.Status == OdfScriptCompilationStatus.Valid)
            {
                return Result(
                    OdfScriptCompilerBackend.LibreOfficeBasic,
                    OdfScriptCompilationStatus.Indeterminate,
                    "ODFSCRIPT_COMPILER_SAFE_PROBE_ONLY");
            }
            return result;
        }
        finally
        {
            if (office is not null)
            {
                TryKill(office);
                office.Dispose();
            }
            DeleteWorkerDirectory(root);
        }
    }

    private static Process StartLibreOffice(string executable, string profile, int port)
    {
        string profileUrl = new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri;
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"-env:UserInstallation={QuoteArgument(profileUrl)} --headless --nologo " +
                "--nodefault --nofirststartwizard --norestore --nolockcheck " +
                $"--accept={QuoteArgument($"socket,host=127.0.0.1,port={port};urp;StarOffice.ServiceManager")}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process? process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfScriptManager_UnsupportedOperation"));
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        return process;
    }

    private static ProcessResult RunProcess(
        string executable,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process? process = Process.Start(startInfo);
        if (process is null)
            return new ProcessResult(exitCode: null, string.Empty, string.Empty, timedOut: false);

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!process.WaitForExit(100))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (elapsed.Elapsed >= timeout)
            {
                TryKill(process);
                return new ProcessResult(
                    exitCode: null,
                    GetCompletedOutput(stdout),
                    GetCompletedOutput(stderr),
                    timedOut: true);
            }
        }

        return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), timedOut: false);
    }

    private static OdfScriptCompilationResult ParseWorkerResult(
        OdfScriptCompilerBackend backend,
        ProcessResult process)
    {
        if (process.TimedOut)
            return Result(backend, OdfScriptCompilationStatus.TimedOut, "ODFSCRIPT_COMPILER_TIMEOUT");

        string? protocol = process.StandardOutput
            .Replace("\r\n", "\n")
            .Split('\n')
            .LastOrDefault(line => line.StartsWith("ODFKIT\t", StringComparison.Ordinal));
        if (protocol is null)
        {
            string detail = LimitDetail(process.StandardError);
            return Result(backend, OdfScriptCompilationStatus.WorkerFailed, "ODFSCRIPT_COMPILER_WORKER_FAILED", detail);
        }

        string[] parts = protocol.Split('\t');
        if (parts.Length >= 2 && parts[1] == "VALID" && process.ExitCode == 0)
            return Result(backend, OdfScriptCompilationStatus.Valid);
        if (parts.Length >= 5 && parts[1] == "INVALID")
        {
            _ = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int line);
            _ = int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int column);
            string detail = DecodeDetail(parts[4]);
            return new OdfScriptCompilationResult(
                backend,
                OdfScriptCompilationStatus.Invalid,
                [new OdfScriptCompilerDiagnostic("ODFSCRIPT_COMPILER_SYNTAX_ERROR", line, column, detail)]);
        }

        return Result(
            backend,
            OdfScriptCompilationStatus.WorkerFailed,
            "ODFSCRIPT_COMPILER_WORKER_FAILED",
            LimitDetail(process.StandardError));
    }

    private static OdfScriptCompilationResult Result(
        OdfScriptCompilerBackend backend,
        OdfScriptCompilationStatus status,
        string? code = null,
        string? detail = null) =>
        new(
            backend,
            status,
            code is null
                ? Array.Empty<OdfScriptCompilerDiagnostic>()
                : [new OdfScriptCompilerDiagnostic(code, 0, 0, detail)]);

    private static string? ResolveExecutable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string path = Path.GetFullPath(value!);
        return File.Exists(path) ? path : null;
    }

    private static string? ResolveLibreOfficePython(string soffice, string? configured)
    {
        string? explicitPath = ResolveExecutable(configured);
        if (explicitPath is not null)
            return explicitPath;
        string directory = Path.GetDirectoryName(soffice)!;
        return new[] { "python.exe", "python", "python.bin" }
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // 關鍵字後的分隔字元不保證是半形空白，也可能是 Tab；一併比對避免中和邏輯被繞過。
    private static bool TryMatchDeclarationKeyword(string declaration, string lower, string keyword, out int nameStart)
    {
        if (lower.Length > keyword.Length &&
            lower.StartsWith(keyword, StringComparison.Ordinal) &&
            char.IsWhiteSpace(declaration[keyword.Length]))
        {
            nameStart = keyword.Length;
            while (nameStart < declaration.Length && char.IsWhiteSpace(declaration[nameStart]))
                nameStart++;
            return true;
        }

        nameStart = 0;
        return false;
    }

    private static string PrepareBasicCompileModule(string source, out string entryPoint)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string declaration = lines[index].TrimStart();
            string lower = declaration.ToLowerInvariant();
            string kind;
            int nameStart;
            if (TryMatchDeclarationKeyword(declaration, lower, "sub", out nameStart))
            {
                kind = "Sub";
            }
            else if (TryMatchDeclarationKeyword(declaration, lower, "function", out nameStart))
            {
                kind = "Function";
            }
            else if (TryMatchDeclarationKeyword(declaration, lower, "public sub", out nameStart) ||
                TryMatchDeclarationKeyword(declaration, lower, "private sub", out nameStart))
            {
                kind = "Sub";
            }
            else if (TryMatchDeclarationKeyword(declaration, lower, "public function", out nameStart) ||
                TryMatchDeclarationKeyword(declaration, lower, "private function", out nameStart))
            {
                kind = "Function";
            }
            else
            {
                continue;
            }

            int nameEnd = declaration.IndexOfAny([' ', '(', '\t'], nameStart);
            if (nameEnd < 0)
                nameEnd = declaration.Length;
            entryPoint = declaration.Substring(nameStart, nameEnd - nameStart);
            int parameters = declaration.IndexOf('(', nameEnd);
            if (entryPoint.Length == 0 ||
                (parameters >= 0 && declaration.IndexOf(')', parameters) > parameters + 1))
                continue;

            string endMarker = kind == "Sub" ? "end sub" : "end function";
            int end = Array.FindIndex(
                lines,
                index + 1,
                line => line.Trim().Equals(endMarker, StringComparison.OrdinalIgnoreCase));
            if (end < 0)
                continue;

            var output = new List<string>(lines.Length + 2);
            output.AddRange(lines.Take(index + 1));
            output.Add("If False Then");
            output.AddRange(lines.Skip(index + 1).Take(end - index - 1));
            output.Add("End If");
            output.AddRange(lines.Skip(end));
            return string.Join(Environment.NewLine, output);
        }

        entryPoint = "OdfKitCompileOnly" + Guid.NewGuid().ToString("N");
        return source + Environment.NewLine +
            $"Sub {entryPoint}{Environment.NewLine}End Sub{Environment.NewLine}";
    }

    private static string CreateWorkerDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "OdfKitScriptCompiler_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteWorkerDirectory(string path)
    {
        for (int attempt = 0; attempt < 20 && Directory.Exists(path); attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
#if NET10_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string GetCompletedOutput(Task<string> output) =>
        output.IsCompleted ? output.GetAwaiter().GetResult() : string.Empty;

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string DecodeDetail(string encoded)
    {
        try
        {
            return LimitDetail(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static string LimitDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return string.Empty;
        string normalized = detail!.Trim();
        return normalized.Substring(0, Math.Min(normalized.Length, MaximumDetailLength));
    }

    private sealed class ProcessResult
    {
        internal ProcessResult(int? exitCode, string standardOutput, string standardError, bool timedOut)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
            TimedOut = timedOut;
        }

        internal int? ExitCode { get; }

        internal string StandardOutput { get; }

        internal string StandardError { get; }

        internal bool TimedOut { get; }
    }

    private const string PythonCompilerWorker =
        "import ast,base64,sys\n" +
        "source=open(sys.argv[1],encoding='utf-8-sig').read()\n" +
        "try:\n" +
        " ast.parse(source,filename='<odf-script>',mode='exec')\n" +
        "except SyntaxError as error:\n" +
        " detail=base64.b64encode(str(error).encode('utf-8')).decode('ascii')\n" +
        " print(f'ODFKIT\\tINVALID\\t{error.lineno or 0}\\t{error.offset or 0}\\t{detail}')\n" +
        "else:\n" +
        " print('ODFKIT\\tVALID')\n";

    private const string LibreOfficeBasicCompilerWorker =
        "import base64,sys,time,uno\n" +
        "def prop(name,value):\n" +
        " item=uno.createUnoStruct('com.sun.star.beans.PropertyValue'); item.Name=name; item.Value=value; return item\n" +
        "port=int(sys.argv[1]); document_path=sys.argv[2]; uri=sys.argv[3]\n" +
        "local=uno.getComponentContext(); resolver=local.ServiceManager.createInstanceWithContext('com.sun.star.bridge.UnoUrlResolver',local)\n" +
        "context=None\n" +
        "for unused in range(200):\n" +
        " try:\n" +
        "  context=resolver.resolve(f'uno:socket,host=127.0.0.1,port={port};urp;StarOffice.ComponentContext'); break\n" +
        " except Exception:\n" +
        "  time.sleep(0.05)\n" +
        "if context is None:\n" +
        " print('ODFKIT\\tWORKER_FAILED'); sys.exit(2)\n" +
        "desktop=context.ServiceManager.createInstanceWithContext('com.sun.star.frame.Desktop',context); document=None\n" +
        "try:\n" +
        " document=desktop.loadComponentFromURL(uno.systemPathToFileUrl(document_path),'_blank',0,(prop('Hidden',True),prop('ReadOnly',False),prop('MacroExecutionMode',4)))\n" +
        " script=document.getScriptProvider().getScript(uri)\n" +
        " script.invoke((),(),())\n" +
        " print('ODFKIT\\tVALID')\n" +
        "except BaseException as error:\n" +
        " detail=base64.b64encode(str(error).encode('utf-8')).decode('ascii')\n" +
        " print(f'ODFKIT\\tINVALID\\t0\\t0\\t{detail}')\n" +
        "finally:\n" +
        " if document is not None: document.close(True)\n" +
        " desktop.terminate()\n";

    private const string MacroSecurityProfile =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<oor:items xmlns:oor=\"http://openoffice.org/2001/registry\">" +
        "<item oor:path=\"/org.openoffice.Office.Common/Security/Scripting\">" +
        "<prop oor:name=\"MacroSecurityLevel\" oor:op=\"fuse\"><value>0</value></prop>" +
        "</item></oor:items>";
}
