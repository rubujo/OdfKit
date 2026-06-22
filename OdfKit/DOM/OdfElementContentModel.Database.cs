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
    /// <returns>資料來源元素</returns>
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
    /// <returns>表單元件容器</returns>
    public DatabaseFormsElement EnsureForms() =>
        DatabaseFormsChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseFormsElement("db"), DatabaseComponentRank.Forms);

    /// <summary>
    /// 取得或建立 <c>db:reports</c> 容器。
    /// </summary>
    /// <returns>報表元件容器</returns>
    public DatabaseReportsElement EnsureReports() =>
        DatabaseReportsChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseReportsElement("db"), DatabaseComponentRank.Reports);

    /// <summary>
    /// 取得或建立 <c>db:queries</c> 容器。
    /// </summary>
    /// <returns>查詢容器</returns>
    public DatabaseQueriesElement EnsureQueries() =>
        DatabaseQueriesChildElements.FirstOrDefault() ?? InsertDatabaseComponent(new DatabaseQueriesElement("db"), DatabaseComponentRank.Queries);

    /// <summary>
    /// 取得或建立 <c>db:table-representations</c> 容器。
    /// </summary>
    /// <returns>資料表描述容器</returns>
    public DatabaseTableRepresentationsElement EnsureTableRepresentations() =>
        DatabaseTableRepresentationsChildElements.FirstOrDefault()
        ?? InsertDatabaseComponent(new DatabaseTableRepresentationsElement("db"), DatabaseComponentRank.TableRepresentations);

    /// <summary>
    /// <c>office:database</c> 元件依 ODF 規格規定的標準順序排名：<c>db:data-source</c>、
    /// <c>db:forms</c>、<c>db:reports</c>、<c>db:queries</c>、<c>db:table-representations</c>、
    /// <c>db:schema</c>。數值越小代表越早出現。
    /// </summary>
    private enum DatabaseComponentRank
    {
        DataSource = 0,
        Forms = 1,
        Reports = 2,
        Queries = 3,
        TableRepresentations = 4,
        Schema = 5
    }

    /// <summary>
    /// 將指定元件插入在第一個「排名晚於 <paramref name="rank"/>」的既有子元素之前，確保插入後
    /// 整體子元素順序仍符合規格規定的標準順序，不論呼叫端以何種順序呼叫各 <c>Ensure*</c> 方法。
    /// </summary>
    /// <typeparam name="TElement">要插入的元件型別</typeparam>
    /// <param name="element">要插入的元件</param>
    /// <param name="rank">該元件在規格標準順序中的排名</param>
    /// <returns>已插入的元件</returns>
    private TElement InsertDatabaseComponent<TElement>(TElement element, DatabaseComponentRank rank)
        where TElement : OdfElement
    {
        OdfNode? insertBefore = Children.FirstOrDefault(child =>
            child is OdfElement candidate &&
            OdfElementContentModel.IsDatabaseComponentContent(candidate) &&
            GetDatabaseComponentRank(candidate) > rank);
        if (insertBefore is null)
        {
            return AppendElement(element);
        }

        return InsertElementBefore(element, insertBefore);
    }

    private static DatabaseComponentRank GetDatabaseComponentRank(OdfElement element) => element switch
    {
        DatabaseDataSourceElement => DatabaseComponentRank.DataSource,
        DatabaseFormsElement => DatabaseComponentRank.Forms,
        DatabaseReportsElement => DatabaseComponentRank.Reports,
        DatabaseQueriesElement => DatabaseComponentRank.Queries,
        DatabaseTableRepresentationsElement => DatabaseComponentRank.TableRepresentations,
        DatabaseSchemaDefinitionElement => DatabaseComponentRank.Schema,
        _ => DatabaseComponentRank.Schema
    };
}
