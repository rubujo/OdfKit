using System.Collections.Generic;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region 表單控制項（Form Controls）

    /// <summary>
    /// Adds a form control to the document (draw:frame + office:forms definition).
    /// 在文件中加入表單控制項（draw:frame + office:forms 定義）。
    /// </summary>
    /// <param name="type">The control type. / 控制項類型。</param>
    /// <param name="name">The control name (a unique identifier). / 控制項名稱（唯一識別字）。</param>
    /// <param name="x">The control's left margin. / 控制項左邊距。</param>
    /// <param name="y">The control's top margin. / 控制項上邊距。</param>
    /// <param name="width">The control width. / 控制項寬度。</param>
    /// <param name="height">The control height. / 控制項高度。</param>
    /// <param name="label">The control's label text (checkbox, button) or default value (text field). / 控制項標籤文字（核取方塊、按鈕）或預設值（文字欄位）。</param>
    /// <param name="listItems">The drop-down list options (valid only for ListBox). / 下拉式清單選項（僅 ListBox 有效）。</param>
    /// <returns>An <see cref="OdfFormControl"/> object describing the new control. / 描述新控制項的 <see cref="OdfFormControl"/> 物件。</returns>
    public OdfFormControl AddFormControl(
        OdfControlType type,
        string name,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string label = "",
        IReadOnlyList<string>? listItems = null) =>
        TextDocumentFormControlsEngine.AddFormControl(MutationContext, type, name, x, y, width, height, label, listItems);

    /// <summary>
    /// Gets all form controls in the document.
    /// 取得文件中所有表單控制項。
    /// </summary>
    /// <returns>The list of controls; an empty list if there is no form. / 控制項清單；若無表單則回傳空清單。</returns>
    public IReadOnlyList<OdfFormControl> GetFormControls() =>
        TextDocumentFormControlsEngine.GetFormControls(BodyTextRoot);

    #endregion
}
