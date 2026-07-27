using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    /// <summary>
    /// 供文字文件核心變更引擎使用的內部協作存取器。
    /// </summary>
    internal TextDocumentCoreCollaborators CoreCollaborators => new(this);

    /// <summary>
    /// 封裝文字文件 DOM 與封裝協作的內部存取器。
    /// </summary>
    internal readonly struct TextDocumentCoreCollaborators
    {
        private readonly TextDocument _document;

        internal TextDocumentCoreCollaborators(TextDocument document) => _document = document;

        internal OdfNode BodyTextRoot => _document.BodyTextRoot;

        internal OdfNode ContentDom => _document.ContentDom;

        internal OdfNode StylesDom => _document.StylesDom;

        internal OdfNode SettingsDom => _document.SettingsDom;

        internal OdfPackage Package => _document.Package;

        internal OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
            => _document.FindOrCreateChild(parent, localName, ns, prefix);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
        internal void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
            => OdfDocumentStyleRemapEngine.RemapStylesInNodes(node, renameMap);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
        internal OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
            => OdfDocumentSettingsEngine.FindOrCreateSettingsNode(root, name);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
        internal OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
            => OdfDocumentSettingsEngine.FindOrCreateMapNode(setNode, name);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
        internal OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
            => OdfDocumentSettingsEngine.FindOrCreateMapEntryNode(mapNode);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
        internal OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
            => OdfDocumentSettingsEngine.FindOrCreateConfigItemNode(entryNode, name, type);
    }
}
