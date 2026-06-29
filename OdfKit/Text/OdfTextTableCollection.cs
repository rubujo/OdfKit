using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf text table collection.
/// 提供表格新增入口。
/// </summary>
public sealed class OdfTextTableCollection : IEnumerable<OdfTextTableInfo>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Provides odf text table collection.
    /// 初始化 <see cref="OdfTextTableCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The value to use. / 所屬文字文件</param>
    public OdfTextTableCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Adds add.
    /// 新增表格。
    /// </summary>
    /// <param name="rows">The numeric value. / 列數</param>
    /// <param name="columns">The numeric value. / 欄數</param>
    /// <returns>The result. / 新增完成的表格</returns>
    public OdfTable Add(int rows, int columns)
    {
        return _document.AddTable(rows, columns);
    }

    /// <summary>
    /// Gets this member.
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
    /// Gets get enumerator.
    /// 取得文字表格摘要列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The result. / 文字表格摘要列舉器</returns>
    public IEnumerator<OdfTextTableInfo> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
