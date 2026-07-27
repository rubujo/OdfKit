using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides high-level lifecycle operations for inline text fields.
/// 提供內嵌文字欄位的高階生命週期操作。
/// </summary>
public partial class TextDocument
{
    /// <summary>
    /// Gets supported inline text fields in document order.
    /// 依文件順序取得支援的內嵌文字欄位。
    /// </summary>
    /// <returns>The inline field list. / 內嵌欄位清單。</returns>
    public IReadOnlyList<OdfTextField> GetTextFields()
    {
        var fields = new List<OdfTextField>();
        CollectTextFields(BodyTextRoot, fields);
        return fields.AsReadOnly();
    }

    /// <summary>
    /// Finds the first inline field of the specified kind.
    /// 尋找指定種類的第一個內嵌欄位。
    /// </summary>
    /// <param name="kind">The field kind. / 欄位種類。</param>
    /// <returns>The matching field, or <see langword="null"/>. / 相符的欄位；若不存在則為 <see langword="null"/>。</returns>
    public OdfTextField? FindTextField(OdfTextFieldKind kind) => FindTextField(kind, null);

    /// <summary>
    /// Finds the first inline field of the specified kind and semantic identifier.
    /// 尋找指定種類與語意識別值的第一個內嵌欄位。
    /// </summary>
    /// <param name="kind">The field kind. / 欄位種類。</param>
    /// <param name="identifier">The exact semantic identifier, or <see langword="null"/> for any. / 精確的語意識別值；若不限制則為 <see langword="null"/>。</param>
    /// <returns>The matching field, or <see langword="null"/>. / 相符的欄位；若不存在則為 <see langword="null"/>。</returns>
    public OdfTextField? FindTextField(OdfTextFieldKind kind, string? identifier)
    {
        foreach (OdfTextField field in GetTextFields())
        {
            if (field.Kind == kind &&
                (identifier is null || string.Equals(field.Identifier, identifier, StringComparison.Ordinal)))
            {
                return field;
            }
        }
        return null;
    }

    /// <summary>
    /// Removes the specified inline field.
    /// 移除指定的內嵌欄位。
    /// </summary>
    /// <param name="field">The field to remove. / 要移除的欄位。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveTextField(OdfTextField field)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(field, nameof(field));
        return IsDescendantOfBody(field.Node) && field.Node.Parent is not null && field.Node.Parent.RemoveChild(field.Node);
    }

    /// <summary>
    /// Removes all supported inline text fields.
    /// 移除所有支援的內嵌文字欄位。
    /// </summary>
    /// <returns>The number of removed fields. / 已移除的欄位數量。</returns>
    public int ClearTextFields()
    {
        List<OdfTextField> fields = [.. GetTextFields()];
        int removed = 0;
        foreach (OdfTextField field in fields)
        {
            if (RemoveTextField(field))
                removed++;
        }
        return removed;
    }

    private static void CollectTextFields(OdfNode root, List<OdfTextField> fields)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text && TryGetTextFieldKind(child.LocalName, out OdfTextFieldKind kind))
                fields.Add(new OdfTextField(child, kind));
            CollectTextFields(child, fields);
        }
    }

    private static bool TryGetTextFieldKind(string localName, out OdfTextFieldKind kind)
    {
        switch (localName)
        {
            case "date":
                kind = OdfTextFieldKind.Date;
                return true;
            case "time":
                kind = OdfTextFieldKind.Time;
                return true;
            case "author-name":
                kind = OdfTextFieldKind.AuthorName;
                return true;
            case "chapter":
                kind = OdfTextFieldKind.Chapter;
                return true;
            case "sequence":
                kind = OdfTextFieldKind.Sequence;
                return true;
            case "reference-ref":
                kind = OdfTextFieldKind.Reference;
                return true;
            case "sequence-ref":
                kind = OdfTextFieldKind.SequenceReference;
                return true;
            case "bookmark-ref":
                kind = OdfTextFieldKind.BookmarkReference;
                return true;
            case "variable-set":
                kind = OdfTextFieldKind.VariableSet;
                return true;
            case "variable-get":
                kind = OdfTextFieldKind.VariableGet;
                return true;
            case "database-display":
                kind = OdfTextFieldKind.DatabaseDisplay;
                return true;
            case "database-next":
                kind = OdfTextFieldKind.DatabaseNext;
                return true;
            case "user-field-get":
                kind = OdfTextFieldKind.UserFieldGet;
                return true;
            case "user-field-input":
                kind = OdfTextFieldKind.UserFieldInput;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private bool IsDescendantOfBody(OdfNode node)
    {
        OdfNode? current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, BodyTextRoot))
                return true;
            current = current.Parent;
        }
        return false;
    }
}
