using System.Collections.Generic;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    #region 表單控制項（Form Controls）
    /// <summary>
    /// Short overload of AddFormControl that accepts type, name, x, y, width, and height; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type、name、x、y、width 與 height；其餘可選參數使用預設值並轉呼叫最長 AddFormControl 多載。
    /// </summary>
    public OdfFormControl AddFormControl(OdfControlType type, string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height) => AddFormControl(type, name, x, y, width, height, "", null);

    /// <summary>
    /// Short overload of AddFormControl that accepts type, name, x, y, width, height, and label; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type、name、x、y、width、height 與 label；其餘可選參數使用預設值並轉呼叫最長 AddFormControl 多載。
    /// </summary>
    public OdfFormControl AddFormControl(OdfControlType type, string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height, string label) => AddFormControl(type, name, x, y, width, height, label, null);


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
    public OdfFormControl AddFormControl(OdfControlType type, string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height, string label, IReadOnlyList<string>? listItems) =>
        TextDocumentFormControlsEngine.AddFormControl(MutationContext, type, name, x, y, width, height, label, listItems);


    /// <summary>
    /// Gets all form controls in the document.
    /// 取得文件中所有表單控制項。
    /// </summary>
    /// <returns>The list of controls; an empty list if there is no form. / 控制項清單；若無表單則回傳空清單。</returns>
    public IReadOnlyList<OdfFormControl> GetFormControls() =>
        TextDocumentFormControlsEngine.GetFormControls(BodyTextRoot);

    /// <summary>
    /// Finds a form control by its exact name.
    /// 依精確名稱尋找表單控制項。
    /// </summary>
    /// <param name="name">The exact control name. / 精確的控制項名稱。</param>
    /// <returns>The matching control snapshot, or <see langword="null"/>. / 相符的控制項快照；若不存在則為 <see langword="null"/>。</returns>
    public OdfFormControl? FindFormControl(string name)
    {
        foreach (OdfFormControl control in GetFormControls())
        {
            if (string.Equals(control.Name, name, System.StringComparison.Ordinal))
                return control;
        }
        return null;
    }

    /// <summary>
    /// Updates the known properties of an existing form control.
    /// 更新現有表單控制項的已知屬性。
    /// </summary>
    /// <param name="name">The exact control name. / 精確的控制項名稱。</param>
    /// <param name="label">The label text. / 標籤文字。</param>
    /// <param name="value">The control value, or <see langword="null"/> to remove it. / 控制項值；若要移除則為 <see langword="null"/>。</param>
    /// <param name="isChecked">Whether a checkbox is checked. / 核取方塊是否已勾選。</param>
    /// <param name="listItems">Replacement list items, or <see langword="null"/> to preserve them. / 替換清單項目；若要保留則為 <see langword="null"/>。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateFormControl(
        string name,
        string label,
        string? value,
        bool isChecked,
        IReadOnlyList<string>? listItems) =>
        TextDocumentFormControlsEngine.UpdateFormControl(BodyTextRoot, name, label, value, isChecked, listItems);

    /// <summary>
    /// Removes a form control definition and every drawing reference to it.
    /// 移除表單控制項定義及所有指向它的繪圖參照。
    /// </summary>
    /// <param name="name">The exact control name. / 精確的控制項名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveFormControl(string name) =>
        TextDocumentFormControlsEngine.RemoveFormControl(BodyTextRoot, name);

    /// <summary>
    /// Removes all supported form controls and their drawing references.
    /// 移除所有支援的表單控制項及其繪圖參照。
    /// </summary>
    /// <returns>The number of removed controls. / 已移除的控制項數量。</returns>
    public int ClearFormControls() =>
        TextDocumentFormControlsEngine.ClearFormControls(BodyTextRoot);

    #endregion
}
