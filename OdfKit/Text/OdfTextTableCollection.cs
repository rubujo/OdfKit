using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 提供表格新增入口。
/// </summary>
public sealed class OdfTextTableCollection : IEnumerable<OdfTextTableInfo>
{
    private readonly TextDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfTextTableCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬文字文件</param>
    public OdfTextTableCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增表格。
    /// </summary>
    /// <param name="rows">列數</param>
    /// <param name="columns">欄數</param>
    /// <returns>新增完成的表格</returns>
    public OdfTable Add(int rows, int columns)
    {
        return _document.AddTable(rows, columns);
    }

    /// <summary>
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
    /// 取得文字表格摘要列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>文字表格摘要列舉器</returns>
    public IEnumerator<OdfTextTableInfo> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
