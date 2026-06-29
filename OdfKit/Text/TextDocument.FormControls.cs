using System.Collections.Generic;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region 表單控制項（Form Controls）

    /// <summary>
    /// Provides add form control.
    /// 在文件中加入表單控制項（draw:frame + office:forms 定義）。
    /// </summary>
    /// <param name="type">The value to use. / 控制項類型</param>
    /// <param name="name">The name or identifier. / 控制項名稱（唯一識別字）</param>
    /// <param name="x">The value to use. / 控制項左邊距</param>
    /// <param name="y">The value to use. / 控制項上邊距</param>
    /// <param name="width">The name or identifier. / 控制項寬度</param>
    /// <param name="height">The numeric value. / 控制項高度</param>
    /// <param name="label">The value to use. / 控制項標籤文字（核取方塊、按鈕）或預設值（文字欄位）</param>
    /// <param name="listItems">The value to use. / 下拉式清單選項（僅 ListBox 有效）</param>
    /// <returns>The result. / 描述新控制項的 <see cref="OdfFormControl"/> 物件</returns>
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
    /// Gets get form controls.
    /// 取得文件中所有表單控制項。
    /// </summary>
    /// <returns>The result. / 控制項清單；若無表單則回傳空清單</returns>
    public IReadOnlyList<OdfFormControl> GetFormControls() =>
        TextDocumentFormControlsEngine.GetFormControls(BodyTextRoot);

    #endregion
}
