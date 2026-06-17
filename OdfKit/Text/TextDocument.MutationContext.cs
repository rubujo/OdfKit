using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 封裝文字文件 DOM 變更所需之內部狀態的協作存取器。
/// </summary>
internal readonly struct TextDocumentMutationContext
{
    private readonly TextDocument _document;

    internal TextDocumentMutationContext(TextDocument document) => _document = document;

    internal OdfNode BodyTextRoot => _document.BodyTextRoot;

    internal OdfNode StylesDom => _document.StylesDom;

    internal bool TrackedChanges => _document.TrackedChanges;

    internal string RecordTrackedChange(string changeType) =>
        _document.RecordTrackedChange(changeType);

    internal string NextFootnoteId() => _document.AllocateFootnoteId();

    internal string NextEndnoteId() => _document.AllocateEndnoteId();

    internal void SetUpdateFieldsWhenOpening(bool update) =>
        TextDocumentSettingsEngine.SetUpdateFieldsWhenOpening(_document.CoreCollaborators, update);
}

public partial class TextDocument
{
    /// <summary>
    /// 供文字文件 DOM 變更引擎使用的內部協作存取器。
    /// </summary>
    internal TextDocumentMutationContext MutationContext => new(this);

    internal string AllocateFootnoteId() => $"ftn{_footnoteCounter++}";

    internal string AllocateEndnoteId() => $"etn{_endnoteCounter++}";
}
