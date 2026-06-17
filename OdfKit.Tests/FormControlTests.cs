using System.IO;
using OdfKit.Core;
using OdfKit.Forms;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 K-1 表單控制項 API（AddFormControl / GetFormControls）的整合測試。
/// </summary>
public class FormControlTests
{
    private static readonly OdfLength Cm1 = OdfLength.FromCentimeters(1);
    private static readonly OdfLength Cm2 = OdfLength.FromCentimeters(2);
    private static readonly OdfLength Cm4 = OdfLength.FromCentimeters(4);
    private static readonly OdfLength Cm08 = OdfLength.FromCentimeters(0.8);

    /// <summary>
    /// 驗證新增核取方塊後，XML 中包含正確的 form:checkbox 與 draw:frame 結構。
    /// </summary>
    [Fact]
    public void AddFormControl_CheckBox_XmlContainsExpectedElements()
    {
        using var doc = TextDocument.Create();

        var ctrl = doc.AddFormControl(OdfControlType.CheckBox, "chk1",
            Cm1, Cm1, Cm4, Cm08, label: "同意條款");

        Assert.Equal(OdfControlType.CheckBox, ctrl.ControlType);
        Assert.Equal("chk1", ctrl.Name);
        Assert.Equal("同意條款", ctrl.Label);

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("content.xml");
        using var reader = new System.IO.StreamReader(stream);
        string xml = reader.ReadToEnd();

        Assert.Contains("form:checkbox", xml);
        Assert.Contains("form:name=\"chk1\"", xml);
        Assert.Contains("form:label=\"同意條款\"", xml);
        Assert.Contains("form:current-state=\"unchecked\"", xml);
        Assert.Contains("draw:control=\"chk1\"", xml);
        Assert.Contains("office:forms", xml);
    }

    /// <summary>
    /// 驗證新增文字欄位後，XML 包含 form:text 元素。
    /// </summary>
    [Fact]
    public void AddFormControl_TextBox_XmlContainsFormText()
    {
        using var doc = TextDocument.Create();
        doc.AddFormControl(OdfControlType.TextBox, "txt1", Cm1, Cm2, Cm4, Cm08, label: "預設文字");

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var cs = pkg.GetEntryStream("content.xml");
        string xml = new System.IO.StreamReader(cs).ReadToEnd();

        Assert.Contains("form:text", xml);
        Assert.Contains("form:name=\"txt1\"", xml);
        Assert.Contains("form:value=\"預設文字\"", xml);
    }

    /// <summary>
    /// 驗證 GetFormControls 能讀回 AddFormControl 寫入的控制項清單。
    /// </summary>
    [Fact]
    public void GetFormControls_ReturnsAllAddedControls()
    {
        using var doc = TextDocument.Create();
        doc.AddFormControl(OdfControlType.CheckBox, "chk1", Cm1, Cm1, Cm4, Cm08, "同意");
        doc.AddFormControl(OdfControlType.TextBox, "txt1", Cm1, Cm2, Cm4, Cm08, "預設");
        doc.AddFormControl(OdfControlType.Button, "btn1", Cm1, Cm1, Cm2, Cm08, "送出");

        var controls = doc.GetFormControls();

        Assert.Equal(3, controls.Count);

        Assert.Equal("chk1", controls[0].Name);
        Assert.Equal(OdfControlType.CheckBox, controls[0].ControlType);
        Assert.Equal("同意", controls[0].Label);
        Assert.False(controls[0].IsChecked);

        Assert.Equal("txt1", controls[1].Name);
        Assert.Equal(OdfControlType.TextBox, controls[1].ControlType);

        Assert.Equal("btn1", controls[2].Name);
        Assert.Equal(OdfControlType.Button, controls[2].ControlType);
        Assert.Equal("送出", controls[2].Label);
    }

    /// <summary>
    /// 驗證 ListBox 控制項包含 form:option 子元素。
    /// </summary>
    [Fact]
    public void AddFormControl_ListBox_XmlContainsOptions()
    {
        using var doc = TextDocument.Create();
        doc.AddFormControl(OdfControlType.ListBox, "lst1", Cm1, Cm1, Cm4, Cm2,
            label: "清單",
            listItems: ["選項 A", "選項 B", "選項 C"]);

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var cs = pkg.GetEntryStream("content.xml");
        string xml = new System.IO.StreamReader(cs).ReadToEnd();

        Assert.Contains("form:listbox", xml);
        Assert.Contains("form:label=\"選項 A\"", xml);
        Assert.Contains("form:label=\"選項 B\"", xml);
        Assert.Contains("form:label=\"選項 C\"", xml);
    }

    /// <summary>
    /// 驗證 GetFormControls 對 ListBox 正確讀回選項清單。
    /// </summary>
    [Fact]
    public void GetFormControls_ListBox_ReturnsCorrectItems()
    {
        using var doc = TextDocument.Create();
        doc.AddFormControl(OdfControlType.ListBox, "lst1", Cm1, Cm1, Cm4, Cm2,
            listItems: ["A", "B", "C"]);

        var controls = doc.GetFormControls();

        Assert.Single(controls);
        Assert.Equal(OdfControlType.ListBox, controls[0].ControlType);
        Assert.Equal(3, controls[0].ListItems.Count);
        Assert.Equal("A", controls[0].ListItems[0]);
        Assert.Equal("B", controls[0].ListItems[1]);
        Assert.Equal("C", controls[0].ListItems[2]);
    }

    /// <summary>
    /// 驗證 GetFormControls 在無表單時回傳空清單。
    /// </summary>
    [Fact]
    public void GetFormControls_NoControls_ReturnsEmpty()
    {
        using var doc = TextDocument.Create();
        var controls = doc.GetFormControls();
        Assert.Empty(controls);
    }
}
