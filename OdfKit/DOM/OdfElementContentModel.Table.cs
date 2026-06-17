using System.Collections.Generic;
using System.Linq;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="TableTableElement"/> 提供 <c>table:table</c> 結構 content model facade。
/// </summary>
public partial class TableTableElement
{
    /// <summary>
    /// 依文件順序列舉 <c>table:table</c> 欄位結構 choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> ColumnStructureChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsTableColumnStructure(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 依文件順序列舉 <c>table:table</c> 列結構 choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> RowStructureChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsTableRowStructure(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 取得或建立 <c>table:table-columns</c> 容器，供後續新增欄定義使用。
    /// </summary>
    /// <returns>表格欄位容器元素。</returns>
    public TableTableColumnsElement EnsureTableColumns()
    {
        TableTableColumnsElement? existing = TableTableColumnsChildElements.FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        return InsertColumnStructure(new TableTableColumnsElement("table"));
    }

    /// <summary>
    /// 在欄位結構區段末尾新增單一欄定義。
    /// </summary>
    /// <returns>新增的 <c>table:table-column</c> 元素。</returns>
    public TableTableColumnElement AppendColumn()
    {
        TableTableColumnsElement columns = EnsureTableColumns();
        return columns.AppendElement(new TableTableColumnElement("table"));
    }

    /// <summary>
    /// 在列結構區段末尾新增表格列。
    /// </summary>
    /// <returns>新增的 <c>table:table-row</c> 元素。</returns>
    public TableTableRowElement AppendRow()
    {
        return AppendRowStructure(new TableTableRowElement("table"));
    }

    /// <summary>
    /// 在列結構區段末尾新增表頭列容器。
    /// </summary>
    /// <returns>新增的 <c>table:table-header-rows</c> 元素。</returns>
    public TableTableHeaderRowsElement AppendHeaderRows()
    {
        return AppendRowStructure(new TableTableHeaderRowsElement("table"));
    }

    /// <summary>
    /// 在欄位結構 choice group 的語意位置插入子元素。
    /// </summary>
    /// <typeparam name="TElement">欄位結構元素型別。</typeparam>
    /// <param name="element">要插入的元素。</param>
    /// <returns>已插入的元素。</returns>
    public TElement InsertColumnStructure<TElement>(TElement element)
        where TElement : OdfElement
    {
        OdfNode? firstRowStructure = Children.FirstOrDefault(child =>
            child is OdfElement rowElement && OdfElementContentModel.IsTableRowStructure(rowElement));
        if (firstRowStructure is null)
        {
            return AppendElement(element);
        }

        return InsertElementBefore(element, firstRowStructure);
    }

    private TElement AppendRowStructure<TElement>(TElement element)
        where TElement : OdfElement
    {
        return AppendElement(element);
    }
}
