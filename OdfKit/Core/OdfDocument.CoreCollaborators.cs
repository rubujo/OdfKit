using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    /// <summary>
    /// 供文件持久化引擎使用的內部協作存取器。
    /// </summary>
    internal OdfDocumentPersistenceCollaborators PersistenceCollaborators => new(this);

    /// <summary>
    /// 封裝文件儲存管線所需 DOM 與封裝存取的內部協作存取器。
    /// </summary>
    internal readonly struct OdfDocumentPersistenceCollaborators
    {
        private readonly OdfDocument _document;

        internal OdfDocumentPersistenceCollaborators(OdfDocument document) => _document = document;

        internal OdfPackage Package => _document.Package;

        internal string SubPath => _document.SubPath;

        internal OdfNode ContentDom => _document.ContentDom;

        internal OdfNode ContentXmlForPersistence => _document.GetContentXmlForPersistence();

        internal OdfNode StylesDom => _document.StylesDom;

        internal OdfNode MetaDom => _document.MetaDom;

        internal OdfNode SettingsDom => _document.SettingsDom;

        internal OdfStyleEngine StyleEngine => _document.StyleEngine;

        internal OdfVersion? TargetVersion => _document.TargetVersion;
    }
}
