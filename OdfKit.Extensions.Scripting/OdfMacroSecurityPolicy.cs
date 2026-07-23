using OdfKit.Compliance;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Identifies capabilities that static macro policy can flag.
/// 識別靜態巨集政策可標示的能力。
/// </summary>
[Flags]
public enum OdfMacroCapability
{
    /// <summary>
    /// No risky capability was identified.
    /// 未識別到高風險能力。
    /// </summary>
    None = 0,

    /// <summary>
    /// Automatic execution through an ODF event binding.
    /// 透過 ODF 事件繫結自動執行。
    /// </summary>
    AutoExecution = 1,

    /// <summary>
    /// File-system access.
    /// 檔案系統存取。
    /// </summary>
    FileSystem = 2,

    /// <summary>
    /// Network access.
    /// 網路存取。
    /// </summary>
    Network = 4,

    /// <summary>
    /// Process or shell execution.
    /// 處理程序或 shell 執行。
    /// </summary>
    ProcessExecution = 8,

    /// <summary>
    /// Creation or lookup of a UNO service.
    /// 建立或查詢 UNO 服務。
    /// </summary>
    UnoService = 16,

    /// <summary>
    /// Dynamic source evaluation.
    /// 動態原始碼求值。
    /// </summary>
    DynamicCode = 32,

    /// <summary>
    /// Every capability currently understood by the policy engine.
    /// 政策引擎目前理解的所有能力。
    /// </summary>
    All = AutoExecution | FileSystem | Network | ProcessExecution | UnoService | DynamicCode
}

/// <summary>
/// Configures conservative static macro capability rules.
/// 設定保守的靜態巨集能力規則。
/// </summary>
public sealed class OdfMacroSecurityPolicy
{
    /// <summary>
    /// Gets or sets capabilities that produce policy violations.
    /// 取得或設定會產生政策違規的能力。
    /// </summary>
    public OdfMacroCapability DeniedCapabilities { get; set; } = OdfMacroCapability.All;

    /// <summary>
    /// Gets case-insensitive UNO service prefixes exempted from <see cref="OdfMacroCapability.UnoService"/>.
    /// 取得不受 <see cref="OdfMacroCapability.UnoService"/> 限制且不分大小寫的 UNO 服務前綴。
    /// </summary>
    public ISet<string> AllowedUnoServicePrefixes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Describes one conservative static macro-policy finding.
/// 描述一項保守的靜態巨集政策發現。
/// </summary>
public sealed class OdfMacroPolicyFinding
{
    internal OdfMacroPolicyFinding(
        string code,
        string path,
        int line,
        OdfMacroCapability capability,
        string? evidence)
    {
        Code = code;
        Path = path;
        Line = line;
        Capability = capability;
        Evidence = evidence;
    }

    /// <summary>
    /// Gets the stable finding code.
    /// 取得穩定的發現代碼。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the package path or event-binding location.
    /// 取得封裝路徑或事件繫結位置。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the one-based source line, or zero for document metadata.
    /// 取得從一開始的原始碼行號；文件中繼資料則為零。
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the capability identified by the finding.
    /// 取得發現所識別的能力。
    /// </summary>
    public OdfMacroCapability Capability { get; }

    /// <summary>
    /// Gets bounded evidence such as a UNO service name.
    /// 取得有界證據，例如 UNO 服務名稱。
    /// </summary>
    public string? Evidence { get; }
}

/// <summary>
/// Reports conservative static macro-policy findings.
/// 回報保守的靜態巨集政策發現。
/// </summary>
public sealed class OdfMacroPolicyResult
{
    internal OdfMacroPolicyResult(IReadOnlyList<OdfMacroPolicyFinding> findings)
    {
        Findings = findings;
    }

    /// <summary>
    /// Gets whether no denied capability was identified.
    /// 取得是否未識別到任何遭拒能力。
    /// </summary>
    public bool IsAllowed => Findings.Count == 0;

    /// <summary>
    /// Gets all policy findings.
    /// 取得所有政策發現。
    /// </summary>
    public IReadOnlyList<OdfMacroPolicyFinding> Findings { get; }
}

/// <summary>
/// Provides static macro-policy evaluation operations.
/// 提供靜態巨集政策評估作業。
/// </summary>
public sealed partial class OdfScriptManager
{
    private static readonly string[] AutoExecutionEvents =
    [
        "load", "open", "start", "new", "create", "view-created", "document-open"
    ];

    /// <summary>
    /// Evaluates document event bindings and package scripts against static capability rules.
    /// 依靜態能力規則評估文件事件繫結與封裝指令碼。
    /// </summary>
    /// <param name="policy">The macro security policy. / 巨集安全政策。</param>
    /// <returns>Conservative findings; an allowed result is not proof that code is safe. / 保守的發現；允許結果不代表程式碼安全。</returns>
    public OdfMacroPolicyResult EvaluateMacroPolicy(OdfMacroSecurityPolicy policy)
    {
        if (policy is null)
        {
            throw new ArgumentNullException(
                nameof(policy),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(policy)));
        }
        if ((policy.DeniedCapabilities & ~OdfMacroCapability.All) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policy),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(policy)));
        }

        List<OdfMacroPolicyFinding> findings = [];
        if (policy.DeniedCapabilities.HasFlag(OdfMacroCapability.AutoExecution))
        {
            foreach (OdfScriptEventBinding binding in GetDocumentEventBindings())
            {
                string eventName = binding.EventName.ToLowerInvariant();
                if (AutoExecutionEvents.Any(value => eventName.Contains(value, StringComparison.Ordinal)))
                {
                    findings.Add(new OdfMacroPolicyFinding(
                        "ODFSCRIPT_POLICY_AUTO_EXECUTION",
                        $"content.xml#event:{binding.Index}",
                        0,
                        OdfMacroCapability.AutoExecution,
                        LimitEvidence(binding.EventName)));
                }
            }
        }

        foreach (OdfPackageScriptEntry entry in GetPackageScripts())
        {
            (OdfScriptSyntaxLanguage language, string source) = ReadPackageScriptSource(entry);
            EvaluateSourcePolicy(entry.Path, language, source, policy, findings);
        }

        return new OdfMacroPolicyResult(findings);
    }

    private static void EvaluateSourcePolicy(
        string path,
        OdfScriptSyntaxLanguage language,
        string source,
        OdfMacroSecurityPolicy policy,
        ICollection<OdfMacroPolicyFinding> findings)
    {
        string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string normalized = lines[index].Trim().ToLowerInvariant();
            if (normalized.Length == 0 || normalized.StartsWith("'", StringComparison.Ordinal) ||
                normalized.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (language == OdfScriptSyntaxLanguage.LibreOfficeBasic)
                EvaluateBasicLine(path, index + 1, normalized, policy, findings);
            else
                EvaluatePythonLine(path, index + 1, normalized, policy, findings);
        }
    }

    private static void EvaluateBasicLine(
        string path,
        int line,
        string source,
        OdfMacroSecurityPolicy policy,
        ICollection<OdfMacroPolicyFinding> findings)
    {
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.ProcessExecution,
            "ODFSCRIPT_POLICY_PROCESS_EXECUTION",
            ["shell(", "shell ", "systemshellexecute"]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.FileSystem,
            "ODFSCRIPT_POLICY_FILE_SYSTEM",
            ["open ", "kill ", "filecopy ", "mkdir ", "rmdir ", "name "]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.Network,
            "ODFSCRIPT_POLICY_NETWORK",
            ["com.sun.star.connection", "com.sun.star.ucb", "http://", "https://"]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.DynamicCode,
            "ODFSCRIPT_POLICY_DYNAMIC_CODE",
            ["execute ", "execute("]);
        AddUnoFinding(path, line, source, policy, findings, "createunoservice");
    }

    private static void EvaluatePythonLine(
        string path,
        int line,
        string source,
        OdfMacroSecurityPolicy policy,
        ICollection<OdfMacroPolicyFinding> findings)
    {
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.ProcessExecution,
            "ODFSCRIPT_POLICY_PROCESS_EXECUTION",
            ["subprocess", "os.system", "os.popen", "startfile(", "pty.spawn"]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.FileSystem,
            "ODFSCRIPT_POLICY_FILE_SYSTEM",
            ["open(", "pathlib", "shutil", "os.remove", "os.unlink", "os.rename", "os.mkdir"]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.Network,
            "ODFSCRIPT_POLICY_NETWORK",
            ["socket", "urllib", "http.client", "requests", "ftplib", "smtplib"]);
        AddFindingWhenMatched(
            path, line, source, policy, findings,
            OdfMacroCapability.DynamicCode,
            "ODFSCRIPT_POLICY_DYNAMIC_CODE",
            ["eval(", "exec(", "compile(", "__import__("]);
        AddUnoFinding(path, line, source, policy, findings, "createinstance");
        AddUnoFinding(path, line, source, policy, findings, "getcomponentcontext");
    }

    private static void AddFindingWhenMatched(
        string path,
        int line,
        string source,
        OdfMacroSecurityPolicy policy,
        ICollection<OdfMacroPolicyFinding> findings,
        OdfMacroCapability capability,
        string code,
        IEnumerable<string> patterns)
    {
        if (!policy.DeniedCapabilities.HasFlag(capability) ||
            !patterns.Any(pattern => source.Contains(pattern, StringComparison.Ordinal)))
        {
            return;
        }

        AddUniqueFinding(findings, new OdfMacroPolicyFinding(code, path, line, capability, evidence: null));
    }

    private static void AddUnoFinding(
        string path,
        int line,
        string source,
        OdfMacroSecurityPolicy policy,
        ICollection<OdfMacroPolicyFinding> findings,
        string marker)
    {
        if (!policy.DeniedCapabilities.HasFlag(OdfMacroCapability.UnoService) ||
            !source.Contains(marker, StringComparison.Ordinal))
        {
            return;
        }

        string? service = ExtractQuotedValue(source, source.IndexOf(marker, StringComparison.Ordinal) + marker.Length);
        if (service is not null && policy.AllowedUnoServicePrefixes.Any(
                prefix => service.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddUniqueFinding(findings, new OdfMacroPolicyFinding(
            "ODFSCRIPT_POLICY_UNO_SERVICE",
            path,
            line,
            OdfMacroCapability.UnoService,
            LimitEvidence(service)));
    }

    private static string? ExtractQuotedValue(string source, int start)
    {
        int single = source.IndexOf('\'', start);
        int doubleQuote = source.IndexOf('"', start);
        int quote = single < 0 ? doubleQuote : doubleQuote < 0 ? single : Math.Min(single, doubleQuote);
        if (quote < 0)
            return null;
        int end = source.IndexOf(source[quote], quote + 1);
        return end > quote ? source.Substring(quote + 1, end - quote - 1) : null;
    }

    private static string? LimitEvidence(string? evidence) =>
        string.IsNullOrWhiteSpace(evidence)
            ? null
            : evidence!.Substring(0, Math.Min(evidence.Length, 256));

    private static void AddUniqueFinding(
        ICollection<OdfMacroPolicyFinding> findings,
        OdfMacroPolicyFinding finding)
    {
        if (!findings.Any(item => item.Path == finding.Path &&
                item.Line == finding.Line &&
                item.Capability == finding.Capability))
        {
            findings.Add(finding);
        }
    }
}
