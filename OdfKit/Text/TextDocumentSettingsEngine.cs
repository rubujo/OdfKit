using OdfKit.Core;
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
        OdfDocumentSettingsEngine.SetUpdateFieldsWhenOpening(ctx.SettingsDom, update);
    }

}
