using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Adds text tables to text documents.
/// 提供表格新增入口。
/// </summary>
public sealed class OdfTextTableCollection : IEnumerable<OdfTextTableInfo>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextTableCollection"/> class.
    /// 初始化 <see cref="OdfTextTableCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfTextTableCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Adds a text table to the collection.
    /// 新增表格。
    /// </summary>
    /// <param name="rows">The row count. / 列數。</param>
    /// <param name="columns">The column count. / 欄數。</param>
    /// <returns>The newly added table. / 新增完成的表格。</returns>
    public OdfTable Add(int rows, int columns)
    {
        return _document.AddTable(rows, columns);
    }

    /// <summary>
    /// Gets a summary list of the top-level text tables in the document body.
    /// 取得文件本文最上層文字表格摘要清單。
    /// </summary>
    public IReadOnlyList<OdfTextTableInfo> Items
    {
        get
        {
            List<OdfTextTableInfo> tables = [];
            foreach (OdfNode child in _document.BodyTextRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "table" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    tables.Add(OdfTextTableInfo.FromNode(child));
                }
            }

            return tables.AsReadOnly();
        }
    }

    /// <summary>
    /// Finds the first top-level table with the specified name.
    /// 查找第一個具有指定名稱的最上層表格。
    /// </summary>
    /// <param name="name">The table name. / 表格名稱。</param>
    /// <returns>The matching table information, or <see langword="null"/> when no match exists. / 符合的表格資訊；若找不到則為 <see langword="null"/>。</returns>
    public OdfTextTableInfo? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(null, nameof(name));
        }

        foreach (OdfTextTableInfo table in Items)
        {
            if (string.Equals(table.Name, name, StringComparison.Ordinal))
            {
                return table;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the specified top-level text table.
    /// 移除指定的最上層文字表格。
    /// </summary>
    /// <param name="table">The table information to remove. / 要移除的表格資訊。</param>
    /// <returns><see langword="true"/> if the table was removed; otherwise, <see langword="false"/>. / 若已移除表格則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Remove(OdfTextTableInfo table)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        return ReferenceEquals(table.Node.Parent, _document.BodyTextRoot) &&
            _document.BodyTextRoot.RemoveChild(table.Node);
    }

    /// <summary>
    /// Removes all top-level text tables while preserving other body content.
    /// 移除所有最上層文字表格，並保留其他本文內容。
    /// </summary>
    public void Clear()
    {
        foreach (OdfTextTableInfo table in Items)
        {
            _document.BodyTextRoot.RemoveChild(table.Node);
        }
    }

    /// <summary>
    /// Gets an enumerator over the text table summaries, for use with LINQ queries.
    /// 取得文字表格摘要列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The text table summary enumerator. / 文字表格摘要列舉器。</returns>
    public IEnumerator<OdfTextTableInfo> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
