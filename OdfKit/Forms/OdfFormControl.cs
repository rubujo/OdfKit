using System;
using System.Collections.Generic;
using OdfKit.Styles;

namespace OdfKit.Forms;

/// <summary>
/// 表單控制項類型。
/// </summary>
public enum OdfControlType
{
    /// <summary>
    /// 文字欄位（form:text）
    /// </summary>
    TextBox,

    /// <summary>
    /// 核取方塊（form:checkbox）
    /// </summary>
    CheckBox,

    /// <summary>
    /// 下拉式清單（form:listbox）
    /// </summary>
    ListBox,

    /// <summary>
    /// 按鈕（form:button）
    /// </summary>
    Button,
}

/// <summary>
/// 表示 ODF 文件中的單一表單控制項。
/// </summary>
public sealed class OdfFormControl
{
    /// <summary>
    /// 取得控制項類型
    /// </summary>
    public OdfControlType ControlType { get; init; }

    /// <summary>
    /// 取得控制項名稱（對應 form:name 屬性）
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 取得控制項標籤文字（對應 form:label 屬性）
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 取得控制項的文字值（TextBox 的 form:value，或按鈕標籤）
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// 取得下拉式清單的選項清單（ListBox 的 form:option 子元素）
    /// </summary>
    public IReadOnlyList<string> ListItems { get; init; } = [];

    /// <summary>
    /// 取得核取方塊是否為勾選狀態
    /// </summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// 取得控制項在頁面上的 X 位置
    /// </summary>
    public OdfLength X { get; init; }

    /// <summary>
    /// 取得控制項在頁面上的 Y 位置
    /// </summary>
    public OdfLength Y { get; init; }

    /// <summary>
    /// 取得控制項寬度
    /// </summary>
    public OdfLength Width { get; init; }

    /// <summary>
    /// 取得控制項高度
    /// </summary>
    public OdfLength Height { get; init; }
}
