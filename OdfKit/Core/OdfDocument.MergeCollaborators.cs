using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    /// <summary>
    /// 供文件合併引擎使用的內部協作存取器。
    /// </summary>
    internal OdfDocument.OdfDocumentMergeCollaborators MergeCollaborators => new(this);

    /// <summary>
    /// 封裝文件合併管線所需 DOM 與樣式引擎存取的內部協作存取器。
    /// </summary>
    internal readonly struct OdfDocumentMergeCollaborators
    {
        private readonly OdfDocument _document;

        internal OdfDocumentMergeCollaborators(OdfDocument document) => _document = document;

        internal OdfNode ContentDom => _document.ContentDom;

        internal OdfNode StylesDom => _document.StylesDom;

        internal OdfStyleEngine StyleEngine => _document.StyleEngine;

        internal OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
            => _document.FindOrCreateChild(parent, localName, ns, prefix);

        internal void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
            => _document.MergeContentNodes(sourceDoc, options, renameMap);

        internal void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
            => OdfDocumentStyleRemapEngine.RemapStylesInNodes(node, renameMap);
    }
}
