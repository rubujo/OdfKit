using System.Collections.Generic;
using System.Linq;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="OfficeDatabaseElement"/> 提供 <c>office:database</c> content model facade。
/// </summary>
public partial class OfficeDatabaseElement
{
    /// <summary>
    /// 依文件順序列舉 <c>office:database</c> 元件 content group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> DatabaseComponentChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsDatabaseComponentContent(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 取得或建立 <c>db:data-source</c> 節點。
    /// </summary>
    /// <returns>資料來源元素。</returns>
    public DatabaseDataSourceElement EnsureDataSource()
    {
        DatabaseDataSourceElement? existing = DatabaseDataSourceChildElements.FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var dataSource = new DatabaseDataSourceElement("db");
        if (Children.Count == 0)
        {
            return AppendElement(dataSource);
        }

        return InsertElementBefore(dataSource, Children[0]);
    }

    /// <summary>
    /// 取得或建立 <c>db:forms</c> 容器。
    /// </summary>
    /// <returns>表單元件容器。</returns>
    public DatabaseFormsElement EnsureForms() =>
        DatabaseFormsChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseFormsElement("db"));

    /// <summary>
    /// 取得或建立 <c>db:reports</c> 容器。
    /// </summary>
    /// <returns>報表元件容器。</returns>
    public DatabaseReportsElement EnsureReports() =>
        DatabaseReportsChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseReportsElement("db"));

    /// <summary>
    /// 取得或建立 <c>db:queries</c> 容器。
    /// </summary>
    /// <returns>查詢容器。</returns>
    public DatabaseQueriesElement EnsureQueries() =>
        DatabaseQueriesChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseQueriesElement("db"));

    /// <summary>
    /// 取得或建立 <c>db:table-representations</c> 容器。
    /// </summary>
    /// <returns>資料表描述容器。</returns>
    public DatabaseTableRepresentationsElement EnsureTableRepresentations() =>
        DatabaseTableRepresentationsChildElements.FirstOrDefault()
        ?? InsertDatabaseComponent(new DatabaseTableRepresentationsElement("db"));

    private TElement InsertDatabaseComponent<TElement>(TElement element)
        where TElement : OdfElement
    {
        OdfNode? insertBefore = Children.FirstOrDefault(child =>
            child is DatabaseReportsElement or DatabaseQueriesElement or
            DatabaseTableRepresentationsElement or DatabaseSchemaDefinitionElement);
        if (insertBefore is null)
        {
            return AppendElement(element);
        }

        return InsertElementBefore(element, insertBefore);
    }
}
