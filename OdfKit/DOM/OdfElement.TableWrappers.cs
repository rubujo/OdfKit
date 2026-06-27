using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Table Wrappers


/// <summary>
/// 表示 ODF 中的 table:table 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableElement(string? prefix = null) : OdfElement("table", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此表格的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Table);
            else
                SetAttributeValue("style-name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:table-row 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableRowElement(string? prefix = null) : OdfElement("table-row", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格列的重複次數。
    /// </summary>
    public int NumberRowsRepeated
    {
        get => GetInt32AttributeValue("number-rows-repeated", OdfNamespaces.Table, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("number-rows-repeated", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
    }
}

/// <summary>
/// 表示 ODF 中的 table:table-cell 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableTableCellElement(string? prefix = null) : OdfElement("table-cell", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此表格儲存格的重複次數。
    /// </summary>
    public int NumberColumnsRepeated
    {
        get => GetInt32AttributeValue("number-columns-repeated", OdfNamespaces.Table, 1, GetDocumentVersion());
        set => SetInt32AttributeValue("number-columns-repeated", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
    }

    /// <summary>
    /// 取得或設定此表格儲存格的值類型。
    /// </summary>
    public string? ValueType
    {
        get => GetAttributeValue("value-type", OdfNamespaces.Office, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("value-type", OdfNamespaces.Office);
            else
                SetAttributeValue("value-type", OdfNamespaces.Office, value, OdfNamespaces.GetPrefix(OdfNamespaces.Office), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此表格儲存格的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => GetAttributeValue("style-name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("style-name", OdfNamespaces.Table);
            else
                SetAttributeValue("style-name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:covered-table-cell 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableCoveredTableCellElement(string? prefix = null) : OdfElement("covered-table-cell", OdfNamespaces.Table, prefix);

/// <summary>
/// 表示 ODF 中的 table:named-range 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableNamedRangeElement(string? prefix = null) : OdfElement("named-range", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此具名範圍的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 table:database-range 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class TableDatabaseRangeElement(string? prefix = null) : OdfElement("database-range", OdfNamespaces.Table, prefix)
{
    /// <summary>
    /// 取得或設定此資料庫範圍的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Table, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Table);
            else
                SetAttributeValue("name", OdfNamespaces.Table, value, OdfNamespaces.GetPrefix(OdfNamespaces.Table), GetDocumentVersion());
        }
    }
}


#endregion
