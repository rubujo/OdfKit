using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// 取得指定查詢的排序陳述式（<c>db:order-statement</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <returns>排序陳述式摘要；若未設定則為 <see langword="null"/></returns>
    public OdfDatabaseQueryStatementInfo? GetQueryOrderStatement(string queryName) =>
        ReadQueryStatement(queryName, "order-statement");

    /// <summary>
    /// 設定指定查詢的排序陳述式（<c>db:order-statement</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <param name="command">排序命令文字（例如 <c>ORDER BY</c> 子句內容）</param>
    /// <param name="applyCommand">選用的套用設定</param>
    /// <returns>新增或更新後的排序陳述式節點</returns>
    public OdfNode SetQueryOrderStatement(string queryName, string command, bool? applyCommand = null) =>
        WriteQueryStatement(
            queryName,
            "order-statement",
            command,
            applyCommand,
            ("filter-statement", DatabaseNamespace), ("columns", DatabaseNamespace), ("update-table", DatabaseNamespace));

    /// <summary>
    /// 取得指定查詢的篩選陳述式（<c>db:filter-statement</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <returns>篩選陳述式摘要；若未設定則為 <see langword="null"/></returns>
    public OdfDatabaseQueryStatementInfo? GetQueryFilterStatement(string queryName) =>
        ReadQueryStatement(queryName, "filter-statement");

    /// <summary>
    /// 設定指定查詢的篩選陳述式（<c>db:filter-statement</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <param name="command">篩選命令文字（例如 <c>WHERE</c> 子句內容）</param>
    /// <param name="applyCommand">選用的套用設定</param>
    /// <returns>新增或更新後的篩選陳述式節點</returns>
    public OdfNode SetQueryFilterStatement(string queryName, string command, bool? applyCommand = null) =>
        WriteQueryStatement(
            queryName,
            "filter-statement",
            command,
            applyCommand,
            ("columns", DatabaseNamespace), ("update-table", DatabaseNamespace));

    /// <summary>
    /// 取得指定查詢的可更新目標資料表名稱（<c>db:update-table</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <returns>目標資料表名稱；若未設定則為 <see langword="null"/></returns>
    public string? GetQueryUpdateTable(string queryName)
    {
        OdfNode query = FindQueryNodeOrThrow(queryName);
        return FindChildElement(query, "update-table", DatabaseNamespace)?.GetAttribute("name", DatabaseNamespace);
    }

    /// <summary>
    /// 設定指定查詢的可更新目標資料表名稱（<c>db:update-table</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <param name="tableName">目標資料表名稱</param>
    /// <returns>新增或更新後的目標資料表節點</returns>
    public OdfNode SetQueryUpdateTable(string queryName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_TargetCannotBeEmpty"), nameof(tableName));
        }

        OdfNode query = FindQueryNodeOrThrow(queryName);
        OdfNode? updateTable = FindChildElement(query, "update-table", DatabaseNamespace);
        if (updateTable is null)
        {
            updateTable = OdfNodeFactory.CreateElement("update-table", DatabaseNamespace, "db");
            query.AppendChild(updateTable);
        }

        updateTable.SetAttribute("name", DatabaseNamespace, tableName, "db");
        return updateTable;
    }

    /// <summary>
    /// 取得指定查詢中可見欄位的名稱清單（<c>db:columns</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <returns>可見欄位名稱清單</returns>
    public IReadOnlyList<string> GetQueryColumns(string queryName)
    {
        OdfNode query = FindQueryNodeOrThrow(queryName);
        OdfNode? columns = FindChildElement(query, "columns", DatabaseNamespace);
        if (columns is null)
        {
            return [];
        }

        List<string> names = [];
        foreach (OdfNode child in columns.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "column" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                string? name = child.GetAttribute("name", DatabaseNamespace);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name!);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// 設定指定查詢的可見欄位清單（<c>db:columns</c>）。
    /// </summary>
    /// <param name="queryName">查詢名稱</param>
    /// <param name="columnNames">欄位名稱清單</param>
    /// <returns>新增或更新後的欄位清單節點</returns>
    /// <exception cref="ArgumentException">當 <paramref name="columnNames"/> 為空時擲出</exception>
    public OdfNode SetQueryColumns(string queryName, IEnumerable<string> columnNames)
    {
        if (columnNames is null)
        {
            throw new ArgumentNullException(nameof(columnNames));
        }

        List<string> names = new(columnNames);
        if (names.Count == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FieldCannotBeEmpty"), nameof(columnNames));
        }

        OdfNode query = FindQueryNodeOrThrow(queryName);
        OdfNode? existing = FindChildElement(query, "columns", DatabaseNamespace);
        if (existing is not null)
        {
            query.RemoveChild(existing);
        }

        OdfNode columns = OdfNodeFactory.CreateElement("columns", DatabaseNamespace, "db");
        foreach (string name in names)
        {
            OdfNode column = OdfNodeFactory.CreateElement("column", DatabaseNamespace, "db");
            column.SetAttribute("name", DatabaseNamespace, name, "db");
            columns.AppendChild(column);
        }

        OdfNode? anchor = FindFirstChildElement(query, ("update-table", DatabaseNamespace));
        if (anchor is null)
        {
            query.AppendChild(columns);
        }
        else
        {
            query.InsertBefore(columns, anchor);
        }

        return columns;
    }

    private OdfDatabaseQueryStatementInfo? ReadQueryStatement(string queryName, string localName)
    {
        OdfNode query = FindQueryNodeOrThrow(queryName);
        OdfNode? statement = FindChildElement(query, localName, DatabaseNamespace);
        if (statement is null)
        {
            return null;
        }

        string command = statement.GetAttribute("command", DatabaseNamespace) ?? string.Empty;
        return new OdfDatabaseQueryStatementInfo(command, ParseNullableBoolean(statement.GetAttribute("apply-command", DatabaseNamespace)));
    }

    private OdfNode WriteQueryStatement(
        string queryName,
        string localName,
        string command,
        bool? applyCommand,
        params (string LocalName, string NamespaceUri)[] insertBeforeCandidates)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DeclarativeCannotBeEmpty"), nameof(command));
        }

        OdfNode query = FindQueryNodeOrThrow(queryName);
        OdfNode? statement = FindChildElement(query, localName, DatabaseNamespace);
        bool isNew = statement is null;
        statement ??= OdfNodeFactory.CreateElement(localName, DatabaseNamespace, "db");
        statement.SetAttribute("command", DatabaseNamespace, command, "db");
        if (applyCommand is not null)
        {
            statement.SetAttribute("apply-command", DatabaseNamespace, applyCommand.Value ? "true" : "false", "db");
        }

        if (isNew)
        {
            OdfNode? anchor = FindFirstChildElement(query, insertBeforeCandidates);
            if (anchor is null)
            {
                query.AppendChild(statement);
            }
            else
            {
                query.InsertBefore(statement, anchor);
            }
        }

        return statement;
    }

    private OdfNode FindQueryNodeOrThrow(string queryName)
    {
        if (string.IsNullOrWhiteSpace(queryName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_5"), nameof(queryName));
        }

        OdfNode? queries = FindChildElement(GetDatabaseNode(), "queries", DatabaseNamespace);
        OdfNode? query = queries is null ? null : FindQueryChild(queries, queryName);
        if (query is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryNotFound", queryName));
        }

        return query;
    }

    private static OdfNode? FindQueryChild(OdfNode queries, string queryName)
    {
        foreach (OdfNode child in queries.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "query" &&
                child.NamespaceUri == DatabaseNamespace &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), queryName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }
}
