using OdfKit.Compliance;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Identifies a scripting language supported by structural syntax diagnostics.
/// 識別結構式語法診斷支援的指令碼語言。
/// </summary>
public enum OdfScriptSyntaxLanguage
{
    /// <summary>
    /// LibreOffice Basic or StarBasic.
    /// LibreOffice Basic 或 StarBasic。
    /// </summary>
    LibreOfficeBasic,

    /// <summary>
    /// Python source embedded for LibreOffice.
    /// 供 LibreOffice 使用的內嵌 Python 原始碼。
    /// </summary>
    Python
}

/// <summary>
/// Defines the severity of a script syntax diagnostic.
/// 定義指令碼語法診斷的嚴重性。
/// </summary>
public enum OdfScriptDiagnosticSeverity
{
    /// <summary>
    /// A warning that may require a language compiler to confirm.
    /// 可能需要語言編譯器確認的警告。
    /// </summary>
    Warning,

    /// <summary>
    /// A structurally invalid construct.
    /// 結構無效的語法結構。
    /// </summary>
    Error
}

/// <summary>
/// Describes a non-executing structural script syntax diagnostic.
/// 描述不執行程式碼的結構式指令碼語法診斷。
/// </summary>
public sealed class OdfScriptSyntaxDiagnostic
{
    internal OdfScriptSyntaxDiagnostic(
        string code,
        OdfScriptDiagnosticSeverity severity,
        int line,
        int column)
    {
        Code = code;
        Severity = severity;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// 取得穩定的診斷代碼。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the diagnostic severity.
    /// 取得診斷嚴重性。
    /// </summary>
    public OdfScriptDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the one-based line number.
    /// 取得從一開始的行號。
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the one-based column number.
    /// 取得從一開始的欄號。
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets a localized summary; the stable <see cref="Code"/> identifies the exact condition.
    /// 取得在地化摘要；確切狀況由穩定的 <see cref="Code"/> 識別。
    /// </summary>
    public string Message => OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidDocumentStructure");
}

/// <summary>
/// Associates structural diagnostics with a package script entry.
/// 將結構式診斷與封裝指令碼項目建立關聯。
/// </summary>
public sealed class OdfPackageScriptDiagnostics
{
    internal OdfPackageScriptDiagnostics(
        string path,
        OdfScriptSyntaxLanguage language,
        IReadOnlyList<OdfScriptSyntaxDiagnostic> diagnostics)
    {
        Path = path;
        Language = language;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the normalized package-relative path.
    /// 取得正規化的封裝相對路徑。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the diagnosed scripting language.
    /// 取得已診斷的指令碼語言。
    /// </summary>
    public OdfScriptSyntaxLanguage Language { get; }

    /// <summary>
    /// Gets the diagnostics without executing the source.
    /// 取得不執行原始碼所得的診斷。
    /// </summary>
    public IReadOnlyList<OdfScriptSyntaxDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Performs conservative structural diagnostics without compiling or executing scripts.
/// 在不編譯或執行指令碼的前提下進行保守的結構式診斷。
/// </summary>
public static class OdfScriptSyntaxValidator
{
    /// <summary>
    /// Diagnoses source text using the selected language profile.
    /// 使用選定的語言 profile 診斷原始碼文字。
    /// </summary>
    /// <param name="source">The source text. / 原始碼文字。</param>
    /// <param name="language">The language profile. / 語言 profile。</param>
    /// <returns>Structural diagnostics; an empty list does not prove compiler acceptance. / 結構式診斷；空清單不代表語言編譯器必然接受。</returns>
    public static IReadOnlyList<OdfScriptSyntaxDiagnostic> Diagnose(
        string source,
        OdfScriptSyntaxLanguage language)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source), OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));
        if (!Enum.IsDefined(typeof(OdfScriptSyntaxLanguage), language))
            throw new ArgumentOutOfRangeException(nameof(language), OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(language)));

        return language == OdfScriptSyntaxLanguage.LibreOfficeBasic
            ? DiagnoseBasic(source)
            : DiagnosePython(source);
    }

    private static IReadOnlyList<OdfScriptSyntaxDiagnostic> DiagnoseBasic(string source)
    {
        var diagnostics = new List<OdfScriptSyntaxDiagnostic>();
        var blocks = new Stack<(string Kind, int Line)>();
        string[] lines = NormalizeLines(source);
        for (int index = 0; index < lines.Length; index++)
        {
            string code = StripBasicComment(lines[index], index + 1, diagnostics).Trim();
            if (code.Length == 0)
                continue;
            string lower = code.ToLowerInvariant();
            if (lower.StartsWith("sub ", StringComparison.Ordinal) || lower.StartsWith("private sub ", StringComparison.Ordinal) || lower.StartsWith("public sub ", StringComparison.Ordinal))
                blocks.Push(("sub", index + 1));
            else if (lower.StartsWith("function ", StringComparison.Ordinal) || lower.StartsWith("private function ", StringComparison.Ordinal) || lower.StartsWith("public function ", StringComparison.Ordinal))
                blocks.Push(("function", index + 1));
            else if (lower == "end sub")
                CloseBasicBlock(blocks, "sub", index + 1, diagnostics);
            else if (lower == "end function")
                CloseBasicBlock(blocks, "function", index + 1, diagnostics);
        }

        foreach ((string _, int line) in blocks)
            diagnostics.Add(Create("ODFSCRIPT_BASIC_UNCLOSED_BLOCK", line, 1));
        return diagnostics;
    }

    private static IReadOnlyList<OdfScriptSyntaxDiagnostic> DiagnosePython(string source)
    {
        var diagnostics = new List<OdfScriptSyntaxDiagnostic>();
        var delimiters = new Stack<(char Value, int Line, int Column)>();
        string? tripleQuote = null;
        string[] lines = NormalizeLines(source);
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            int leading = 0;
            bool sawSpace = false;
            bool sawTab = false;
            while (leading < line.Length && (line[leading] == ' ' || line[leading] == '\t'))
            {
                sawSpace |= line[leading] == ' ';
                sawTab |= line[leading] == '\t';
                leading++;
            }
            if (sawSpace && sawTab)
                diagnostics.Add(Create("ODFSCRIPT_PYTHON_MIXED_INDENTATION", lineIndex + 1, 1));

            bool startedInsideTripleQuote = tripleQuote is not null;
            ScanPythonLine(line, lineIndex + 1, delimiters, diagnostics, ref tripleQuote);
            string trimmed = StripPythonComment(line).TrimEnd();
            if (!startedInsideTripleQuote
                && tripleQuote is null
                && RequiresPythonColon(trimmed)
                && !trimmed.EndsWith(":", StringComparison.Ordinal))
                diagnostics.Add(Create("ODFSCRIPT_PYTHON_MISSING_COLON", lineIndex + 1, Math.Max(1, line.Length)));
        }

        foreach ((char _, int line, int column) in delimiters)
            diagnostics.Add(Create("ODFSCRIPT_PYTHON_UNCLOSED_DELIMITER", line, column));
        if (tripleQuote is not null)
            diagnostics.Add(Create("ODFSCRIPT_PYTHON_UNTERMINATED_STRING", lines.Length, 1));
        return diagnostics;
    }

    private static string StripBasicComment(
        string line,
        int lineNumber,
        ICollection<OdfScriptSyntaxDiagnostic> diagnostics)
    {
        bool inString = false;
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                if (inString && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index++;
                    continue;
                }
                inString = !inString;
            }
            else if (line[index] == '\'' && !inString)
            {
                return line.Substring(0, index);
            }
        }
        if (inString)
            diagnostics.Add(Create("ODFSCRIPT_BASIC_UNTERMINATED_STRING", lineNumber, line.Length));
        return line;
    }

    private static void CloseBasicBlock(
        Stack<(string Kind, int Line)> blocks,
        string expected,
        int line,
        ICollection<OdfScriptSyntaxDiagnostic> diagnostics)
    {
        if (blocks.Count == 0 || blocks.Peek().Kind != expected)
        {
            diagnostics.Add(Create("ODFSCRIPT_BASIC_UNEXPECTED_END", line, 1));
            return;
        }
        blocks.Pop();
    }

    private static void ScanPythonLine(
        string line,
        int lineNumber,
        Stack<(char Value, int Line, int Column)> delimiters,
        ICollection<OdfScriptSyntaxDiagnostic> diagnostics,
        ref string? tripleQuote)
    {
        char quote = '\0';
        bool escaped = false;
        for (int index = 0; index < line.Length; index++)
        {
            if (tripleQuote is not null)
            {
                int closing = line.IndexOf(tripleQuote, index, StringComparison.Ordinal);
                if (closing < 0)
                    return;
                index = closing + 2;
                tripleQuote = null;
                continue;
            }

            char value = line[index];
            if (quote != '\0')
            {
                if (escaped)
                    escaped = false;
                else if (value == '\\')
                    escaped = true;
                else if (value == quote)
                    quote = '\0';
                continue;
            }
            if (value == '#')
                break;
            if (index + 2 < line.Length
                && (line.Substring(index, 3) == "'''" || line.Substring(index, 3) == "\"\"\""))
            {
                tripleQuote = line.Substring(index, 3);
                index += 2;
                continue;
            }
            if (value is '\'' or '"')
            {
                quote = value;
                continue;
            }
            if (value is '(' or '[' or '{')
                delimiters.Push((value, lineNumber, index + 1));
            else if (value is ')' or ']' or '}')
            {
                char expected = value == ')' ? '(' : value == ']' ? '[' : '{';
                if (delimiters.Count == 0 || delimiters.Peek().Value != expected)
                    diagnostics.Add(Create("ODFSCRIPT_PYTHON_UNEXPECTED_DELIMITER", lineNumber, index + 1));
                else
                    delimiters.Pop();
            }
        }
        if (quote != '\0')
            diagnostics.Add(Create("ODFSCRIPT_PYTHON_UNTERMINATED_STRING", lineNumber, line.Length));
    }

    private static string StripPythonComment(string line)
    {
        char quote = '\0';
        bool escaped = false;
        for (int index = 0; index < line.Length; index++)
        {
            char value = line[index];
            if (quote != '\0')
            {
                if (escaped)
                    escaped = false;
                else if (value == '\\')
                    escaped = true;
                else if (value == quote)
                    quote = '\0';
            }
            else if (value is '\'' or '"')
                quote = value;
            else if (value == '#')
                return line.Substring(0, index);
        }
        return line;
    }

    private static bool RequiresPythonColon(string line)
    {
        string trimmed = line.TrimStart();
        string[] prefixes = ["def ", "class ", "if ", "elif ", "else", "for ", "while ", "try", "except", "finally", "with ", "match ", "case "];
        return prefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string[] NormalizeLines(string source) =>
        source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static OdfScriptSyntaxDiagnostic Create(string code, int line, int column) =>
        new(code, OdfScriptDiagnosticSeverity.Error, line, Math.Max(1, column));
}
