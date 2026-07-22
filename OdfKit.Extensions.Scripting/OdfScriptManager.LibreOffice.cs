using System.Text;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Provides LibreOffice package-profile script management operations.
/// 提供 LibreOffice 封裝 profile 的指令碼管理作業。
/// </summary>
public sealed partial class OdfScriptManager
{
    private const string BasicRoot = "Basic/";
    private const string PythonRoot = "Scripts/python/";
    private const string BasicContainerPath = "Basic/script-lc.xml";
    private const string LibraryNamespace = "http://openoffice.org/2000/library";
    private const string LibreOfficeScriptNamespace = "http://openoffice.org/2000/script";
    private const long MaxMetadataCharacters = 4L * 1024 * 1024;

    /// <summary>
    /// Lists implementation-specific LibreOffice scripting entries.
    /// 列出實作特定的 LibreOffice 指令碼項目。
    /// </summary>
    /// <returns>A path-sorted snapshot of recognized package entries. / 依路徑排序的已辨識封裝項目快照。</returns>
    public IReadOnlyList<OdfPackageScriptEntry> GetPackageScripts()
    {
        EnsurePackageScriptsSupported();
        List<OdfPackageScriptEntry> result = [];
        foreach (OdfPackage.OdfPackageEntryInfo entry in _package.GetEntries().OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            OdfPackageScriptKind? kind = ClassifyPackageScript(entry.Path);
            if (kind.HasValue)
                result.Add(new OdfPackageScriptEntry(entry.Path, kind.Value));
        }

        return result;
    }

    /// <summary>
    /// Reads a recognized LibreOffice scripting entry as UTF-8 text.
    /// 將已辨識的 LibreOffice 指令碼項目讀取為 UTF-8 文字。
    /// </summary>
    /// <param name="path">The package-relative scripting entry path. / 指令碼項目的封裝相對路徑。</param>
    /// <returns>The UTF-8 text stored in the entry. / 項目中儲存的 UTF-8 文字。</returns>
    public string ReadPackageScript(string path)
    {
        EnsurePackageScriptsSupported();
        string normalized = ValidateScriptingEntryPath(path);
        return Encoding.UTF8.GetString(_package.ReadEntry(normalized));
    }

    /// <summary>
    /// Diagnoses recognized LibreOffice Basic and Python entries without executing them.
    /// 在不執行程式碼的前提下診斷已辨識的 LibreOffice Basic 與 Python 項目。
    /// </summary>
    /// <returns>One result for each recognized package script. / 每個已辨識封裝指令碼各一筆結果。</returns>
    public IReadOnlyList<OdfPackageScriptDiagnostics> DiagnosePackageScripts()
    {
        List<OdfPackageScriptDiagnostics> results = [];
        foreach (OdfPackageScriptEntry entry in GetPackageScripts())
        {
            (OdfScriptSyntaxLanguage language, string source) = ReadPackageScriptSource(entry);

            results.Add(new OdfPackageScriptDiagnostics(
                entry.Path,
                language,
                OdfScriptSyntaxValidator.Diagnose(source, language)));
        }
        return results;
    }

    private (OdfScriptSyntaxLanguage Language, string Source) ReadPackageScriptSource(
        OdfPackageScriptEntry entry)
    {
        string stored = ReadPackageScript(entry.Path);
        return entry.Kind == OdfPackageScriptKind.LibreOfficeBasicModule
            ? (OdfScriptSyntaxLanguage.LibreOfficeBasic,
                LoadXml(Encoding.UTF8.GetBytes(stored)).Root?.Value ?? string.Empty)
            : (OdfScriptSyntaxLanguage.Python, stored);
    }

    /// <summary>
    /// Adds or replaces a LibreOffice Basic module and maintains its library metadata.
    /// 新增或取代 LibreOffice Basic 模組，並維護其程式庫中繼資料。
    /// </summary>
    /// <param name="libraryName">The Basic library name. / Basic 程式庫名稱。</param>
    /// <param name="moduleName">The Basic module name. / Basic 模組名稱。</param>
    /// <param name="source">The StarBasic source text. / StarBasic 原始碼文字。</param>
    public void AddOrUpdateLibreOfficeBasicModule(string libraryName, string moduleName, string source)
    {
        ValidatePathSegment(libraryName, nameof(libraryName));
        ValidatePathSegment(moduleName, nameof(moduleName));
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));
        }
        EnsurePackageScriptsSupported();

        string libraryPath = $"Basic/{libraryName}/script-lb.xml";
        string modulePath = $"Basic/{libraryName}/{moduleName}.xml";

        XDocument container = LoadOrCreateBasicContainer();
        EnsureBasicLibraryReference(container, libraryName);
        XDocument library = LoadOrCreateBasicLibrary(libraryPath, libraryName);
        EnsureBasicModuleReference(library, moduleName);

        _package.WriteEntry(BasicContainerPath, SerializeXml(container), "text/xml");
        _package.WriteEntry(libraryPath, SerializeXml(library), "text/xml");
        _package.WriteEntry(modulePath, CreateBasicModule(moduleName, source), "text/xml");
        RemoveInvalidatedMacroSignatures();
    }

    /// <summary>
    /// Removes a LibreOffice Basic module and its library metadata reference.
    /// 移除 LibreOffice Basic 模組及其程式庫中繼資料參照。
    /// </summary>
    /// <param name="libraryName">The Basic library name. / Basic 程式庫名稱。</param>
    /// <param name="moduleName">The Basic module name. / Basic 模組名稱。</param>
    /// <returns><see langword="true"/> if the module existed and was removed; otherwise, <see langword="false"/>. / 若模組存在且已移除則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveLibreOfficeBasicModule(string libraryName, string moduleName)
    {
        ValidatePathSegment(libraryName, nameof(libraryName));
        ValidatePathSegment(moduleName, nameof(moduleName));
        EnsurePackageScriptsSupported();

        string libraryPath = $"Basic/{libraryName}/script-lb.xml";
        string modulePath = $"Basic/{libraryName}/{moduleName}.xml";
        XDocument? library = null;
        XElement? libraryRoot = null;
        if (_package.HasEntry(libraryPath))
        {
            library = LoadXml(_package.ReadEntry(libraryPath));
            XNamespace libraryNs = LibraryNamespace;
            libraryRoot = GetRequiredMetadataRoot(library, libraryNs + "library");
        }

        if (!_package.RemoveEntry(modulePath))
            return false;

        if (library is not null && libraryRoot is not null)
        {
            XNamespace libraryNs = LibraryNamespace;
            libraryRoot.Elements(libraryNs + "element")
                .FirstOrDefault(element => (string?)element.Attribute(libraryNs + "name") == moduleName)
                ?.Remove();
            _package.WriteEntry(libraryPath, SerializeXml(library), "text/xml");
        }

        RemoveInvalidatedMacroSignatures();
        return true;
    }

    /// <summary>
    /// Removes a LibreOffice Basic library and all modules contained by its package folder.
    /// 移除 LibreOffice Basic 程式庫及其封裝資料夾內的所有模組。
    /// </summary>
    /// <param name="libraryName">The Basic library name. / Basic 程式庫名稱。</param>
    /// <returns><see langword="true"/> if any package state was removed; otherwise, <see langword="false"/>. / 若已移除任何封裝狀態則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveLibreOfficeBasicLibrary(string libraryName)
    {
        ValidatePathSegment(libraryName, nameof(libraryName));
        EnsurePackageScriptsSupported();

        XDocument? container = null;
        XElement? containerRoot = null;
        if (_package.HasEntry(BasicContainerPath))
        {
            container = LoadXml(_package.ReadEntry(BasicContainerPath));
            XNamespace libraryNs = LibraryNamespace;
            containerRoot = GetRequiredMetadataRoot(container, libraryNs + "libraries");
        }

        bool removed = false;
        string prefix = $"Basic/{libraryName}/";
        string[] paths = _package.GetEntries()
            .Select(entry => entry.Path)
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        foreach (string path in paths)
            removed |= _package.RemoveEntry(path);

        if (container is not null && containerRoot is not null)
        {
            XNamespace libraryNs = LibraryNamespace;
            XElement? reference = containerRoot.Elements(libraryNs + "library")
                .FirstOrDefault(element => (string?)element.Attribute(libraryNs + "name") == libraryName);
            if (reference is not null)
            {
                reference.Remove();
                removed = true;
                _package.WriteEntry(BasicContainerPath, SerializeXml(container), "text/xml");
            }
        }

        if (removed)
            RemoveInvalidatedMacroSignatures();
        return removed;
    }

    /// <summary>
    /// Adds or replaces an embedded LibreOffice Python module.
    /// 新增或取代內嵌的 LibreOffice Python 模組。
    /// </summary>
    /// <param name="relativePath">The path below <c>Scripts/python/</c>, including the <c>.py</c> extension. / 位於 <c>Scripts/python/</c> 下且包含 <c>.py</c> 副檔名的路徑。</param>
    /// <param name="source">The Python source text. / Python 原始碼文字。</param>
    /// <returns>The normalized package-relative path. / 正規化後的封裝相對路徑。</returns>
    public string AddOrUpdateLibreOfficePythonModule(string relativePath, string source)
    {
        if (relativePath is null)
        {
            throw new ArgumentNullException(
                nameof(relativePath),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(relativePath)));
        }
        if (source is null)
        {
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));
        }
        EnsurePackageScriptsSupported();

        string path = NormalizePythonPath(relativePath);
        _package.WriteEntry(path, Encoding.UTF8.GetBytes(source), "text/x-python");
        RemoveInvalidatedMacroSignatures();
        return path;
    }

    /// <summary>
    /// Removes an embedded LibreOffice Python module.
    /// 移除內嵌的 LibreOffice Python 模組。
    /// </summary>
    /// <param name="relativePath">The path below <c>Scripts/python/</c>, including the <c>.py</c> extension. / 位於 <c>Scripts/python/</c> 下且包含 <c>.py</c> 副檔名的路徑。</param>
    /// <returns><see langword="true"/> if the module was removed; otherwise, <see langword="false"/>. / 若模組已移除則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveLibreOfficePythonModule(string relativePath)
    {
        if (relativePath is null)
        {
            throw new ArgumentNullException(
                nameof(relativePath),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(relativePath)));
        }
        EnsurePackageScriptsSupported();

        bool removed = _package.RemoveEntry(NormalizePythonPath(relativePath));
        if (removed)
            RemoveInvalidatedMacroSignatures();
        return removed;
    }

    private static OdfPackageScriptKind? ClassifyPackageScript(string path)
    {
        if (path.StartsWith(PythonRoot, StringComparison.Ordinal) &&
            path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            return OdfPackageScriptKind.LibreOfficePythonModule;
        }

        if (path.StartsWith(BasicRoot, StringComparison.Ordinal) &&
            path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("script-lc.xml", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("script-lb.xml", StringComparison.OrdinalIgnoreCase))
        {
            return OdfPackageScriptKind.LibreOfficeBasicModule;
        }

        return null;
    }

    private string ValidateScriptingEntryPath(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(
                nameof(path),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(path)));
        }
        string normalized = OdfPackage.SanitizeEntryName(path);
        if (ClassifyPackageScript(normalized) is null)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(path)),
                nameof(path));
        }
        return normalized;
    }

    private void EnsurePackageScriptsSupported()
    {
        if (!Capabilities.SupportsPackageScripts)
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfScriptManager_UnsupportedOperation"));
    }

    private static string NormalizePythonPath(string relativePath)
    {
        string combined = OdfPackage.SanitizeEntryName(PythonRoot + relativePath);
        if (!combined.StartsWith(PythonRoot, StringComparison.Ordinal) ||
            !combined.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
            combined.Length == PythonRoot.Length + 3)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(relativePath)),
                nameof(relativePath));
        }

        return combined;
    }

    private static void ValidatePathSegment(string? value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(
                parameterName,
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", parameterName));
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", parameterName),
                parameterName);
        }
        if (value is "." or ".." || value.Any(character =>
                character is '/' or '\\' or ':' || char.IsControl(character)))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", parameterName),
                parameterName);
        }
    }

    private XDocument LoadOrCreateBasicContainer()
    {
        if (_package.HasEntry(BasicContainerPath))
            return LoadXml(_package.ReadEntry(BasicContainerPath));

        XNamespace libraryNs = LibraryNamespace;
        XNamespace xlinkNs = OdfNamespaces.XLink;
        return new XDocument(
            new XElement(
                libraryNs + "libraries",
                new XAttribute(XNamespace.Xmlns + "library", libraryNs),
                new XAttribute(XNamespace.Xmlns + "xlink", xlinkNs)));
    }

    private XDocument LoadOrCreateBasicLibrary(string path, string libraryName)
    {
        if (_package.HasEntry(path))
            return LoadXml(_package.ReadEntry(path));

        XNamespace libraryNs = LibraryNamespace;
        return new XDocument(
            new XElement(
                libraryNs + "library",
                new XAttribute(XNamespace.Xmlns + "library", libraryNs),
                new XAttribute(libraryNs + "name", libraryName),
                new XAttribute(libraryNs + "readonly", "false"),
                new XAttribute(libraryNs + "passwordprotected", "false")));
    }

    private static void EnsureBasicLibraryReference(XDocument container, string libraryName)
    {
        XNamespace libraryNs = LibraryNamespace;
        XElement root = GetRequiredMetadataRoot(container, libraryNs + "libraries");
        if (!root.Elements(libraryNs + "library")
            .Any(element => (string?)element.Attribute(libraryNs + "name") == libraryName))
        {
            root.Add(
                new XElement(
                    libraryNs + "library",
                    new XAttribute(libraryNs + "name", libraryName),
                    new XAttribute(libraryNs + "link", "false")));
        }
    }

    private static void EnsureBasicModuleReference(XDocument library, string moduleName)
    {
        XNamespace libraryNs = LibraryNamespace;
        XElement root = GetRequiredMetadataRoot(library, libraryNs + "library");
        if (!root.Elements(libraryNs + "element")
            .Any(element => (string?)element.Attribute(libraryNs + "name") == moduleName))
        {
            root.Add(new XElement(libraryNs + "element", new XAttribute(libraryNs + "name", moduleName)));
        }
    }

    private static byte[] CreateBasicModule(string moduleName, string source)
    {
        XNamespace scriptNs = LibreOfficeScriptNamespace;
        var document = new XDocument(
            new XElement(
                scriptNs + "module",
                new XAttribute(XNamespace.Xmlns + "script", scriptNs),
                new XAttribute(scriptNs + "name", moduleName),
                new XAttribute(scriptNs + "language", "StarBasic"),
                new XAttribute(scriptNs + "moduleType", "normal"),
                source));
        return SerializeXml(document);
    }

    private static XDocument LoadXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxMetadataCharacters
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static XElement GetRequiredMetadataRoot(XDocument document, XName expectedName)
    {
        XElement? root = document.Root;
        if (root is null || root.Name != expectedName)
            throw new XmlException(OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidDocumentStructure"));
        return root;
    }

    private static byte[] SerializeXml(XDocument document)
    {
        using var stream = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            CloseOutput = false
        }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }
}
