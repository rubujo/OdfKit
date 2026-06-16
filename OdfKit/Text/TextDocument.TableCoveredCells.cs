using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Table covered cells omissions


    /// <summary>
    /// 新增一個表格項目至文件本文結尾。
    /// </summary>
    /// <param name="rows">表格的列數</param>
    /// <param name="cols">表格的欄數</param>
    /// <returns>新建立的表格物件</returns>
    public OdfTable AddTable(int rows, int cols)
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        BodyTextRoot.AppendChild(table);
        return new OdfTable(table, rows, cols, this);
    }


    #endregion
}
