using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OfficeSpreadsheetElement API.
/// 為 <see cref="OfficeSpreadsheetElement"/> 提供 <c>office:spreadsheet</c> content model facade。
/// </summary>
public partial class OfficeSpreadsheetElement
{
    /// <summary>
    /// Provides the member member.
    /// 依文件順序列舉 <c>office:spreadsheet</c> 試算表 content group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> SpreadsheetTableChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsSpreadsheetTableContent(element))
                {
                    yield return element;
                }
            }
        }
    }
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public TableTableElement AppendTable() => AppendTable(null);


    /// <summary>
    /// Executes the AppendTable operation.
    /// 在 <c>office:spreadsheet</c> 末尾新增工作表表格。
    /// </summary>
    /// <param name="name">選用的表格名稱</param>
    /// <returns>新增的 <c>table:table</c> 元素</returns>
    public TableTableElement AppendTable(string? name)
    {
        TableTableElement table = AppendElement(new TableTableElement("table"));
        if (name is not null)
        {
            table.Name = name;
        }

        return table;
    }

}
