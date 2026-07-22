using OdfKit.Compliance;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Identifies how an ODF event listener locates its script.
/// 識別 ODF 事件監聽器定位指令碼的方式。
/// </summary>
public enum OdfScriptTargetKind
{
    /// <summary>
    /// Uses the language-dependent <c>script:macro-name</c> attribute.
    /// 使用依指令碼語言定義的 <c>script:macro-name</c> 屬性。
    /// </summary>
    MacroName,

    /// <summary>
    /// Uses an IRI stored in the <c>xlink:href</c> attribute.
    /// 使用儲存於 <c>xlink:href</c> 屬性的 IRI。
    /// </summary>
    Uri
}

/// <summary>
/// Describes a direct <c>office:script</c> child in an ODF document.
/// 描述 ODF 文件中直接隸屬於 <c>office:scripts</c> 的 <c>office:script</c>。
/// </summary>
public sealed class OdfInlineScript
{
    internal OdfInlineScript(int index, string language, string source)
    {
        Index = index;
        Language = language;
        Source = source;
    }

    /// <summary>
    /// Gets the zero-based position used by update and removal operations.
    /// 取得供更新及移除作業使用的零起始位置。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the application-defined scripting language name.
    /// 取得由應用程式定義的指令碼語言名稱。
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the textual script source.
    /// 取得文字形式的指令碼原始碼。
    /// </summary>
    public string Source { get; }
}

/// <summary>
/// Describes a document-level ODF event binding.
/// 描述文件層級的 ODF 事件繫結。
/// </summary>
public sealed class OdfScriptEventBinding
{
    internal OdfScriptEventBinding(
        int index,
        string eventName,
        string language,
        string target,
        OdfScriptTargetKind targetKind)
    {
        Index = index;
        EventName = eventName;
        Language = language;
        Target = target;
        TargetKind = targetKind;
    }

    /// <summary>
    /// Gets the zero-based position used by update and removal operations.
    /// 取得供更新及移除作業使用的零起始位置。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the application-defined event name.
    /// 取得由應用程式定義的事件名稱。
    /// </summary>
    public string EventName { get; }

    /// <summary>
    /// Gets the scripting language name.
    /// 取得指令碼語言名稱。
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the macro name or IRI target.
    /// 取得巨集名稱或 IRI 目標。
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets the representation used for the target.
    /// 取得目標所使用的表示方式。
    /// </summary>
    public OdfScriptTargetKind TargetKind { get; }
}

/// <summary>
/// Identifies a scripting artifact stored as an ODF package entry.
/// 識別儲存為 ODF 封裝項目的指令碼成品。
/// </summary>
public enum OdfPackageScriptKind
{
    /// <summary>
    /// A LibreOffice Basic module.
    /// LibreOffice Basic 模組。
    /// </summary>
    LibreOfficeBasicModule,

    /// <summary>
    /// A LibreOffice embedded Python module.
    /// LibreOffice 內嵌 Python 模組。
    /// </summary>
    LibreOfficePythonModule,

    /// <summary>
    /// An unclassified implementation-specific scripting entry.
    /// 尚未分類的實作特定指令碼項目。
    /// </summary>
    Other
}

/// <summary>
/// Describes a scripting artifact stored in the ODF package.
/// 描述儲存於 ODF 封裝中的指令碼成品。
/// </summary>
public sealed class OdfPackageScriptEntry
{
    internal OdfPackageScriptEntry(string path, OdfPackageScriptKind kind)
    {
        Path = path;
        Kind = kind;
    }

    /// <summary>
    /// Gets the normalized package-relative path.
    /// 取得正規化的封裝相對路徑。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the detected scripting artifact kind.
    /// 取得偵測到的指令碼成品種類。
    /// </summary>
    public OdfPackageScriptKind Kind { get; }
}

/// <summary>
/// Reports scripting capabilities for the current package representation.
/// 回報目前封裝表示方式可使用的指令碼能力。
/// </summary>
public sealed class OdfScriptingCapabilities
{
    internal OdfScriptingCapabilities(OdfVersion version, bool supportsPackageEntries)
    {
        Version = version;
        SupportsInlineScripts = version is OdfVersion.Odf10 or OdfVersion.Odf11 or OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14;
        SupportsDocumentEventBindings = SupportsInlineScripts;
        SupportsPackageScripts = supportsPackageEntries && SupportsInlineScripts;
    }

    /// <summary>
    /// Gets the detected ODF version.
    /// 取得偵測到的 ODF 版本。
    /// </summary>
    public OdfVersion Version { get; }

    /// <summary>
    /// Gets whether standard inline script management is supported.
    /// 取得是否支援標準內嵌指令碼管理。
    /// </summary>
    public bool SupportsInlineScripts { get; }

    /// <summary>
    /// Gets whether document event binding management is supported.
    /// 取得是否支援文件事件繫結管理。
    /// </summary>
    public bool SupportsDocumentEventBindings { get; }

    /// <summary>
    /// Gets whether implementation-specific package script entries are supported.
    /// 取得是否支援實作特定的封裝指令碼項目。
    /// </summary>
    public bool SupportsPackageScripts { get; }
}
