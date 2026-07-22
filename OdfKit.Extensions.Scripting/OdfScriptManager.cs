using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Manages ODF 1.0 through 1.4 scripts without compiling or executing them.
/// 管理 ODF 1.0 至 1.4 指令碼，且不會編譯或執行其內容。
/// </summary>
public sealed partial class OdfScriptManager
{
    private const string ContentPath = "content.xml";
    private const string MacroSignaturePath = "META-INF/macrosignatures.xml";
    private const string LegacyMacroSignaturePath = "macrosignatures.xml";

    private readonly OdfPackage _package;
    private readonly bool _supportsPackageScripts;

    internal OdfScriptManager(OdfPackage package, bool supportsPackageScripts = true)
    {
        _package = package;
        _supportsPackageScripts = supportsPackageScripts;
    }

    /// <summary>
    /// Gets the scripting capabilities detected from the current document representation.
    /// 取得從目前文件表示方式偵測到的指令碼能力。
    /// </summary>
    public OdfScriptingCapabilities Capabilities
    {
        get
        {
            OdfNode root = ReadContentRoot();
            bool supportsPackageScripts = _supportsPackageScripts && root.LocalName != "document";
            return new OdfScriptingCapabilities(root.GetDocumentVersion(), supportsPackageScripts);
        }
    }

    /// <summary>
    /// Lists standard inline scripts in document order.
    /// 依文件順序列出標準內嵌指令碼。
    /// </summary>
    /// <returns>A snapshot of direct <c>office:script</c> children. / 直接隸屬的 <c>office:script</c> 子元素快照。</returns>
    public IReadOnlyList<OdfInlineScript> GetInlineScripts()
    {
        OdfNode root = ReadContentRoot();
        EnsureSupportedVersion(root);
        OdfNode? scripts = FindScripts(root);
        if (scripts is null)
            return Array.Empty<OdfInlineScript>();

        List<OdfInlineScript> result = [];
        foreach (OdfNode child in scripts.Children)
        {
            if (IsElement(child, "script", OdfNamespaces.Office))
            {
                result.Add(new OdfInlineScript(
                    result.Count,
                    child.GetAttribute("language", OdfNamespaces.Script) ?? string.Empty,
                    child.TextContent));
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a textual standard inline script.
    /// 新增文字形式的標準內嵌指令碼。
    /// </summary>
    /// <param name="language">The application-defined language name. / 由應用程式定義的語言名稱。</param>
    /// <param name="source">The script source text. / 指令碼原始碼文字。</param>
    /// <returns>The zero-based index assigned to the script. / 指派給指令碼的零起始索引。</returns>
    public int AddInlineScript(string language, string source)
    {
        ValidateRequired(language, nameof(language));
        if (source is null)
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));

        int addedIndex = -1;
        MutateContent(root =>
        {
            OdfNode scripts = GetOrCreateScripts(root);
            addedIndex = CountChildren(scripts, "script", OdfNamespaces.Office);
            var script = new OdfNode(OdfNodeType.Element, "script", OdfNamespaces.Office, "office");
            script.SetAttribute("language", OdfNamespaces.Script, language, "script");
            script.TextContent = source;
            InsertScriptBeforeEventListeners(scripts, script);
        });
        return addedIndex;
    }

    /// <summary>
    /// Replaces a textual standard inline script.
    /// 取代文字形式的標準內嵌指令碼。
    /// </summary>
    /// <param name="index">The zero-based script index. / 指令碼的零起始索引。</param>
    /// <param name="language">The application-defined language name. / 由應用程式定義的語言名稱。</param>
    /// <param name="source">The replacement source text. / 要取代的原始碼文字。</param>
    public void UpdateInlineScript(int index, string language, string source)
    {
        ValidateRequired(language, nameof(language));
        if (source is null)
            throw new ArgumentNullException(
                nameof(source),
                OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(source)));

        MutateContent(root =>
        {
            OdfNode script = GetIndexedChild(GetRequiredScripts(root, index), "script", OdfNamespaces.Office, index);
            script.SetAttribute("language", OdfNamespaces.Script, language, "script");
            script.TextContent = source;
        });
    }

    /// <summary>
    /// Removes a standard inline script.
    /// 移除標準內嵌指令碼。
    /// </summary>
    /// <param name="index">The zero-based script index. / 指令碼的零起始索引。</param>
    public void RemoveInlineScript(int index)
    {
        MutateContent(root =>
        {
            OdfNode scripts = GetRequiredScripts(root, index);
            OdfNode script = GetIndexedChild(scripts, "script", OdfNamespaces.Office, index);
            scripts.RemoveChild(script);
            RemoveEmptyScriptContainers(root, scripts);
        });
    }

    /// <summary>
    /// Lists document-level standard event bindings in document order.
    /// 依文件順序列出文件層級的標準事件繫結。
    /// </summary>
    /// <returns>A snapshot of document event bindings. / 文件事件繫結快照。</returns>
    public IReadOnlyList<OdfScriptEventBinding> GetDocumentEventBindings()
    {
        OdfNode root = ReadContentRoot();
        EnsureSupportedVersion(root);
        OdfNode? listeners = FindScripts(root)?.FindChildElement("event-listeners", OdfNamespaces.Office);
        if (listeners is null)
            return Array.Empty<OdfScriptEventBinding>();

        List<OdfScriptEventBinding> result = [];
        foreach (OdfNode child in listeners.Children)
        {
            if (!IsElement(child, "event-listener", OdfNamespaces.Script))
                continue;

            string? macroName = child.GetAttribute("macro-name", OdfNamespaces.Script);
            string? uri = child.GetAttribute("href", OdfNamespaces.XLink);
            result.Add(new OdfScriptEventBinding(
                result.Count,
                child.GetAttribute("event-name", OdfNamespaces.Script) ?? string.Empty,
                child.GetAttribute("language", OdfNamespaces.Script) ?? string.Empty,
                macroName ?? uri ?? string.Empty,
                macroName is not null ? OdfScriptTargetKind.MacroName : OdfScriptTargetKind.Uri));
        }

        return result;
    }

    /// <summary>
    /// Adds a document-level standard event binding.
    /// 新增文件層級的標準事件繫結。
    /// </summary>
    /// <param name="eventName">The application-defined event name. / 由應用程式定義的事件名稱。</param>
    /// <param name="language">The scripting language name. / 指令碼語言名稱。</param>
    /// <param name="target">The macro name or IRI target. / 巨集名稱或 IRI 目標。</param>
    /// <param name="targetKind">The target representation. / 目標表示方式。</param>
    /// <returns>The zero-based index assigned to the binding. / 指派給繫結的零起始索引。</returns>
    public int AddDocumentEventBinding(
        string eventName,
        string language,
        string target,
        OdfScriptTargetKind targetKind)
    {
        ValidateRequired(eventName, nameof(eventName));
        ValidateRequired(language, nameof(language));
        ValidateRequired(target, nameof(target));
        ValidateTargetKind(targetKind);

        int addedIndex = -1;
        MutateContent(root =>
        {
            OdfNode scripts = GetOrCreateScripts(root);
            OdfNode listeners = scripts.FindChildElement("event-listeners", OdfNamespaces.Office)
                ?? AppendElement(scripts, "event-listeners", OdfNamespaces.Office, "office");
            addedIndex = CountChildren(listeners, "event-listener", OdfNamespaces.Script);
            OdfNode listener = CreateEventListener(eventName, language, target, targetKind);
            listeners.AppendChild(listener);
        });
        return addedIndex;
    }

    /// <summary>
    /// Replaces a document-level standard event binding.
    /// 取代文件層級的標準事件繫結。
    /// </summary>
    /// <param name="index">The zero-based binding index. / 繫結的零起始索引。</param>
    /// <param name="eventName">The application-defined event name. / 由應用程式定義的事件名稱。</param>
    /// <param name="language">The scripting language name. / 指令碼語言名稱。</param>
    /// <param name="target">The macro name or IRI target. / 巨集名稱或 IRI 目標。</param>
    /// <param name="targetKind">The target representation. / 目標表示方式。</param>
    public void UpdateDocumentEventBinding(
        int index,
        string eventName,
        string language,
        string target,
        OdfScriptTargetKind targetKind)
    {
        ValidateRequired(eventName, nameof(eventName));
        ValidateRequired(language, nameof(language));
        ValidateRequired(target, nameof(target));
        ValidateTargetKind(targetKind);

        MutateContent(root =>
        {
            OdfNode scripts = GetRequiredScripts(root, index);
            OdfNode listeners = scripts.FindChildElement("event-listeners", OdfNamespaces.Office)
                ?? throw CreateIndexOutOfRangeException(index);
            OdfNode existing = GetIndexedChild(listeners, "event-listener", OdfNamespaces.Script, index);
            listeners.InsertBefore(CreateEventListener(eventName, language, target, targetKind), existing);
            listeners.RemoveChild(existing);
        });
    }

    /// <summary>
    /// Removes a document-level standard event binding.
    /// 移除文件層級的標準事件繫結。
    /// </summary>
    /// <param name="index">The zero-based binding index. / 繫結的零起始索引。</param>
    public void RemoveDocumentEventBinding(int index)
    {
        MutateContent(root =>
        {
            OdfNode scripts = GetRequiredScripts(root, index);
            OdfNode listeners = scripts.FindChildElement("event-listeners", OdfNamespaces.Office)
                ?? throw CreateIndexOutOfRangeException(index);
            OdfNode listener = GetIndexedChild(listeners, "event-listener", OdfNamespaces.Script, index);
            listeners.RemoveChild(listener);
            if (listeners.Children.Count == 0)
                scripts.RemoveChild(listeners);
            RemoveEmptyScriptContainers(root, scripts);
        });
    }

    private static void ValidateTargetKind(OdfScriptTargetKind targetKind)
    {
        if (targetKind is not OdfScriptTargetKind.MacroName and not OdfScriptTargetKind.Uri)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetKind),
                targetKind,
                OdfLocalizer.GetMessage("Err_OdfScriptManager_InvalidArgument", nameof(targetKind)));
        }
    }

    private static OdfNode CreateEventListener(
        string eventName,
        string language,
        string target,
        OdfScriptTargetKind targetKind)
    {
        var listener = new OdfNode(OdfNodeType.Element, "event-listener", OdfNamespaces.Script, "script");
        listener.SetAttribute("event-name", OdfNamespaces.Script, eventName, "script");
        listener.SetAttribute("language", OdfNamespaces.Script, language, "script");
        if (targetKind == OdfScriptTargetKind.MacroName)
        {
            listener.SetAttribute("macro-name", OdfNamespaces.Script, target, "script");
        }
        else
        {
            listener.SetAttribute("href", OdfNamespaces.XLink, target, "xlink");
            listener.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        }

        return listener;
    }

    private void MutateContent(Action<OdfNode> mutation)
    {
        OdfNode root = ReadContentRoot();
        EnsureSupportedVersion(root);
        mutation(root);

        using var output = new MemoryStream();
        OdfXmlWriter.Write(root, output);
        _package.WriteEntry(ContentPath, output.ToArray(), "text/xml");
        RemoveInvalidatedMacroSignatures();
    }

    private OdfNode ReadContentRoot()
    {
        using Stream stream = _package.GetEntryStream(ContentPath);
        return OdfXmlReader.Parse(stream);
    }

    private static void EnsureSupportedVersion(OdfNode root)
    {
        if (root.GetDocumentVersion() is not (OdfVersion.Odf10 or OdfVersion.Odf11 or OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfScriptManager_UnsupportedVersion"));
    }

    private static OdfNode? FindScripts(OdfNode root) => root.FindChildElement("scripts", OdfNamespaces.Office);

    private static OdfNode GetRequiredScripts(OdfNode root, int index) =>
        FindScripts(root) ?? throw CreateIndexOutOfRangeException(index);

    private static OdfNode GetOrCreateScripts(OdfNode root)
    {
        OdfNode? existing = FindScripts(root);
        if (existing is not null)
            return existing;

        var scripts = new OdfNode(OdfNodeType.Element, "scripts", OdfNamespaces.Office, "office");
        OdfNode? insertBefore = root.Children.FirstOrDefault(child =>
            child.NodeType == OdfNodeType.Element &&
            child.NamespaceUri == OdfNamespaces.Office &&
            child.LocalName is "font-face-decls" or "styles" or "automatic-styles" or "master-styles" or "body");
        if (insertBefore is null)
            root.AppendChild(scripts);
        else
            root.InsertBefore(scripts, insertBefore);
        return scripts;
    }

    private static OdfNode AppendElement(OdfNode parent, string localName, string namespaceUri, string prefix)
    {
        var child = new OdfNode(OdfNodeType.Element, localName, namespaceUri, prefix);
        parent.AppendChild(child);
        return child;
    }

    private static void InsertScriptBeforeEventListeners(OdfNode scripts, OdfNode script)
    {
        OdfNode? listeners = scripts.FindChildElement("event-listeners", OdfNamespaces.Office);
        if (listeners is null)
            scripts.AppendChild(script);
        else
            scripts.InsertBefore(script, listeners);
    }

    private static int CountChildren(OdfNode parent, string localName, string namespaceUri) =>
        parent.Children.Count(child => IsElement(child, localName, namespaceUri));

    private static OdfNode GetIndexedChild(OdfNode parent, string localName, string namespaceUri, int index)
    {
        if (index < 0)
            throw CreateIndexOutOfRangeException(index);

        int current = 0;
        foreach (OdfNode child in parent.Children)
        {
            if (!IsElement(child, localName, namespaceUri))
                continue;
            if (current++ == index)
                return child;
        }

        throw CreateIndexOutOfRangeException(index);
    }

    private static bool IsElement(OdfNode node, string localName, string namespaceUri) =>
        node.NodeType == OdfNodeType.Element &&
        node.LocalName == localName &&
        node.NamespaceUri == namespaceUri;

    private static void RemoveEmptyScriptContainers(OdfNode root, OdfNode scripts)
    {
        if (scripts.Children.Count == 0)
            root.RemoveChild(scripts);
    }

    private static void ValidateRequired(string? value, string parameterName)
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
    }

    private static ArgumentOutOfRangeException CreateIndexOutOfRangeException(int index) =>
        new(
            nameof(index),
            index,
            OdfLocalizer.GetMessage("Err_OdfScriptManager_IndexOutOfRange", index));

    private void RemoveInvalidatedMacroSignatures()
    {
        _package.RemoveEntry(MacroSignaturePath);
        _package.RemoveEntry(LegacyMacroSignaturePath);
    }
}
