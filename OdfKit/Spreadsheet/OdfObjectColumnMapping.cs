using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines one object property to spreadsheet column mapping.
/// 定義單一物件屬性與試算表欄位的對應。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfObjectColumnMapping"/> class.
/// 初始化 <see cref="OdfObjectColumnMapping"/> 類別的新執行個體。
/// </remarks>
/// <param name="propertyName">The property name. / 屬性名稱。</param>
public sealed class OdfObjectColumnMapping(string propertyName)
{
    /// <summary>
    /// Gets the property name.
    /// 取得屬性名稱。
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Gets or sets the header name.
    /// 取得或設定標題名稱。
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the optional column order.
    /// 取得或設定選用欄位順序。
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// Gets or sets whether the property is ignored.
    /// 取得或設定是否忽略此屬性。
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets whether the spreadsheet column is required.
    /// 取得或設定是否需要此試算表欄位。
    /// </summary>
    public bool RequiredColumn { get; set; }

    /// <summary>
    /// Gets or sets whether cell values for this property are required.
    /// 取得或設定此屬性的儲存格值是否必填。
    /// </summary>
    public bool RequiredValue { get; set; }

    /// <summary>
    /// Gets or sets the default value used when the cell is empty.
    /// 取得或設定儲存格為空時使用的預設值。
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the default value factory used when the cell is empty.
    /// 取得或設定儲存格為空時使用的預設值工廠。
    /// </summary>
    public Func<object?>? DefaultValueFactory { get; set; }

    /// <summary>
    /// Gets header aliases used when reading objects.
    /// 取得讀取物件時使用的標題別名。
    /// </summary>
    public IList<string> Aliases { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the column formatting options.
    /// 取得或設定欄位格式選項。
    /// </summary>
    public OdfObjectColumnFormat? Format { get; set; }
}
