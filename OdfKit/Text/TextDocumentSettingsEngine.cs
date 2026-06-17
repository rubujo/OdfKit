using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件設定 DOM 引擎（內部協作者）。
/// </summary>
internal static class TextDocumentSettingsEngine
{
    /// <summary>
    /// 設定開啟文件時是否自動更新欄位。
    /// </summary>
    internal static void SetUpdateFieldsWhenOpening(TextDocument.TextDocumentCoreCollaborators ctx, bool update)
    {
        OdfNode sc = ctx.FindOrCreateSettingsNode(ctx.SettingsDom, "view-settings");
        OdfNode map = ctx.FindOrCreateMapNode(sc, "Views");
        OdfNode entry = ctx.FindOrCreateMapEntryNode(map);
        OdfNode item = ctx.FindOrCreateConfigItemNode(entry, "UpdateFieldsWhenOpening", "boolean");
        item.TextContent = update ? "true" : "false";
    }
}
