using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Identifies a supported inline text-field kind.
/// 識別支援的內嵌文字欄位種類。
/// </summary>
public enum OdfTextFieldKind
{
    /// <summary>
    /// Date field.
    /// 日期欄位。
    /// </summary>
    Date,
    /// <summary>
    /// Time field.
    /// 時間欄位。
    /// </summary>
    Time,
    /// <summary>
    /// Author-name field.
    /// 作者名稱欄位。
    /// </summary>
    AuthorName,
    /// <summary>
    /// Chapter field.
    /// 章節欄位。
    /// </summary>
    Chapter,
    /// <summary>
    /// Sequence field.
    /// 序號欄位。
    /// </summary>
    Sequence,
    /// <summary>
    /// Generic reference field.
    /// 一般參照欄位。
    /// </summary>
    Reference,
    /// <summary>
    /// Sequence-reference field.
    /// 序號參照欄位。
    /// </summary>
    SequenceReference,
    /// <summary>
    /// Bookmark-reference field.
    /// 書籤參照欄位。
    /// </summary>
    BookmarkReference,
    /// <summary>
    /// Variable-set field.
    /// 變數設定欄位。
    /// </summary>
    VariableSet,
    /// <summary>
    /// Variable-get field.
    /// 變數讀取欄位。
    /// </summary>
    VariableGet,
    /// <summary>
    /// Database-display field.
    /// 資料庫顯示欄位。
    /// </summary>
    DatabaseDisplay,
    /// <summary>
    /// Database-next field.
    /// 資料庫下一筆欄位。
    /// </summary>
    DatabaseNext,
    /// <summary>
    /// User-field-get field.
    /// 使用者欄位讀取欄位。
    /// </summary>
    UserFieldGet,
    /// <summary>
    /// User-field-input field.
    /// 使用者欄位輸入欄位。
    /// </summary>
    UserFieldInput,
}

/// <summary>
/// Represents an inline text field linked to its document node.
/// 表示與文件節點連結的內嵌文字欄位。
/// </summary>
public sealed class OdfTextField
{
    internal OdfTextField(OdfNode node, OdfTextFieldKind kind)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Kind = kind;
    }

    internal OdfNode Node { get; }

    /// <summary>
    /// Gets the field kind.
    /// 取得欄位種類。
    /// </summary>
    public OdfTextFieldKind Kind { get; }

    /// <summary>
    /// Gets or sets the field name.
    /// 取得或設定欄位名稱。
    /// </summary>
    public string? Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Text);
        set => SetTextAttribute("name", value);
    }

    /// <summary>
    /// Gets or sets the referenced name.
    /// 取得或設定被參照名稱。
    /// </summary>
    public string? ReferenceName
    {
        get => Node.GetAttribute("ref-name", OdfNamespaces.Text);
        set => SetTextAttribute("ref-name", value);
    }

    /// <summary>
    /// Gets or sets the reference format token.
    /// 取得或設定參照格式詞彙。
    /// </summary>
    public string? ReferenceFormat
    {
        get => Node.GetAttribute("reference-format", OdfNamespaces.Text);
        set => SetTextAttribute("reference-format", value);
    }

    /// <summary>
    /// Gets or sets the number format token.
    /// 取得或設定數字格式詞彙。
    /// </summary>
    public string? NumberFormat
    {
        get => Node.GetAttribute("num-format", OdfNamespaces.Style);
        set => SetAttribute("num-format", OdfNamespaces.Style, "style", value);
    }

    /// <summary>
    /// Gets or sets the database table name.
    /// 取得或設定資料庫資料表名稱。
    /// </summary>
    public string? TableName
    {
        get => Node.GetAttribute("table-name", OdfNamespaces.Text);
        set => SetTextAttribute("table-name", value);
    }

    /// <summary>
    /// Gets or sets the database column name.
    /// 取得或設定資料庫欄位名稱。
    /// </summary>
    public string? ColumnName
    {
        get => Node.GetAttribute("column-name", OdfNamespaces.Text);
        set => SetTextAttribute("column-name", value);
    }

    /// <summary>
    /// Gets or sets the database name.
    /// 取得或設定資料庫名稱。
    /// </summary>
    public string? DatabaseName
    {
        get => Node.GetAttribute("database-name", OdfNamespaces.Text);
        set => SetTextAttribute("database-name", value);
    }

    /// <summary>
    /// Gets or sets the database table type.
    /// 取得或設定資料庫資料表類型。
    /// </summary>
    public string? TableType
    {
        get => Node.GetAttribute("table-type", OdfNamespaces.Text);
        set => SetTextAttribute("table-type", value);
    }

    /// <summary>
    /// Gets or sets the field condition.
    /// 取得或設定欄位條件。
    /// </summary>
    public string? Condition
    {
        get => Node.GetAttribute("condition", OdfNamespaces.Text);
        set => SetTextAttribute("condition", value);
    }

    /// <summary>
    /// Gets or sets the visible field text while preserving element children.
    /// 取得或設定可見欄位文字，同時保留元素子節點。
    /// </summary>
    public string DisplayText
    {
        get => Node.TextContent;
        set
        {
            foreach (OdfNode child in new List<OdfNode>(Node.Children))
            {
                if (child.NodeType == OdfNodeType.Text)
                    Node.RemoveChild(child);
            }
            if (!string.IsNullOrEmpty(value))
                Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = value });
        }
    }

    /// <summary>
    /// Gets the best available semantic identifier for this field.
    /// 取得此欄位可用的最佳語意識別值。
    /// </summary>
    public string? Identifier => Name ?? ReferenceName ?? TableName;

    private void SetTextAttribute(string localName, string? value) =>
        SetAttribute(localName, OdfNamespaces.Text, "text", value);

    private void SetAttribute(string localName, string namespaceUri, string prefix, string? value)
    {
        if (value is null)
            Node.RemoveAttribute(localName, namespaceUri);
        else
            Node.SetAttribute(localName, namespaceUri, value, prefix);
    }
}
