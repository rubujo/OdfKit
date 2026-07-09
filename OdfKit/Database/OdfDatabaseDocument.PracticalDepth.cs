using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// Configures the data source connection and returns this document for chaining.
    /// 設定資料來源連線並傳回目前文件以支援鏈式呼叫。
    /// </summary>
    /// <param name="href">The connection resource path or URL. / 連線資源路徑或 URL。</param>
    /// <returns>The current database document. / 目前資料庫文件。</returns>
    public OdfDatabaseDocument ConfigureConnection(string href)
    {
        SetConnection(href);
        return this;
    }
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfNode AddParameterizedQuery(string name, string command, IEnumerable<OdfDatabaseQueryParameter> parameters) => AddParameterizedQuery(name, command, parameters, null);


    /// <summary>
    /// Adds a parameterized query description without executing SQL.
    /// 新增參數化查詢描述，但不執行 SQL。
    /// </summary>
    /// <param name="name">The query name. / 查詢名稱。</param>
    /// <param name="command">The query command or SQL content. / 查詢命令或 SQL 內容。</param>
    /// <param name="parameters">The query parameter metadata. / 查詢參數中繼資料。</param>
    /// <param name="title">The optional display title. / 選用顯示標題。</param>
    /// <returns>The added query node. / 新增的查詢節點。</returns>
    public OdfNode AddParameterizedQuery(string name, string command, IEnumerable<OdfDatabaseQueryParameter> parameters, string? title)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        string description = "Parameters: " + string.Join(
            ", ",
            parameters.Select(parameter =>
                string.IsNullOrWhiteSpace(parameter.Description)
                    ? parameter.Name + ":" + parameter.Type
                    : parameter.Name + ":" + parameter.Type + " (" + parameter.Description + ")"));
        return AddQuery(name, command, title, description, escapeProcessing: true);
    }


    /// <summary>
    /// Adds a table schema description as a table representation command hint.
    /// 以資料表表示命令提示新增資料表 schema 描述。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <param name="columns">The column declarations. / 欄位宣告。</param>
    /// <returns>The added table node. / 新增的資料表節點。</returns>
    public OdfNode AddTableSchema(string name, IEnumerable<string> columns)
    {
        if (columns is null)
        {
            throw new ArgumentNullException(nameof(columns));
        }

        string command = string.Join(", ", columns.Where(column => !string.IsNullOrWhiteSpace(column)));
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_5"), nameof(columns));
        }

        return AddTable(name, command);
    }
}
