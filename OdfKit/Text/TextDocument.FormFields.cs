using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    private OdfFormFieldCollection? _formFields;

    /// <summary>
    /// 取得文件中可依名稱填入的文字與表單欄位集合。
    /// </summary>
    public OdfFormFieldCollection FormFields => _formFields ??= new OdfFormFieldCollection(this);
}

/// <summary>
/// 提供文字文件表單欄位的名稱索引與安全填值 API。
/// </summary>
public sealed class OdfFormFieldCollection
{
    private readonly TextDocument _document;

    internal OdfFormFieldCollection(TextDocument document) => _document = document;

    /// <summary>
    /// 以欄位名稱取得表單欄位 facade。
    /// </summary>
    /// <param name="key">欄位名稱</param>
    /// <returns>指定名稱的欄位 facade；可透過 <see cref="OdfFormField.Exists"/> 判斷目前文件是否包含該欄位</returns>
    public OdfFormField this[string key] => new(_document, key);

    /// <summary>
    /// 判斷文件中是否存在指定名稱的欄位。
    /// </summary>
    /// <param name="key">欄位名稱</param>
    /// <returns>若文件中存在該欄位則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool Contains(string key) => TextDocumentFormFieldBinder.Contains(_document.BodyTextRoot, key);

    /// <summary>
    /// 嘗試將指定值寫入欄位。
    /// </summary>
    /// <param name="key">欄位名稱</param>
    /// <param name="value">要寫入的值</param>
    /// <returns>若找到並更新至少一個欄位則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool TrySetValue(string key, string? value) =>
        TextDocumentFormFieldBinder.TrySetValue(_document.BodyTextRoot, key, value);
}

/// <summary>
/// 表示文字文件中可填入的單一欄位 facade。
/// </summary>
public sealed class OdfFormField
{
    private readonly TextDocument _document;

    internal OdfFormField(TextDocument document, string key)
    {
        _document = document;
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// 取得欄位名稱。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 取得目前文件是否包含此欄位。
    /// </summary>
    public bool Exists => TextDocumentFormFieldBinder.Contains(_document.BodyTextRoot, Key);

    /// <summary>
    /// 取得或設定欄位值；若欄位不存在，設定時不會建立新欄位。
    /// </summary>
    public string? Value
    {
        get => TextDocumentFormFieldBinder.GetValue(_document.BodyTextRoot, Key);
        set => TextDocumentFormFieldBinder.TrySetValue(_document.BodyTextRoot, Key, value);
    }

    /// <summary>
    /// 嘗試設定欄位值。
    /// </summary>
    /// <param name="value">要寫入的值</param>
    /// <returns>若找到並更新至少一個欄位則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool TrySetValue(string? value) => TextDocumentFormFieldBinder.TrySetValue(_document.BodyTextRoot, Key, value);
}

internal static class TextDocumentFormFieldBinder
{
    private static readonly HashSet<string> SupportedFormControls = new(StringComparer.Ordinal)
    {
        "checkbox",
        "formatted-text",
        "generic-control",
        "listbox",
        "password",
        "text",
        "textarea",
    };

    internal static bool Contains(OdfNode root, string key)
    {
        foreach (OdfNode _ in FindMatchingNodes(root, key))
        {
            return true;
        }

        return false;
    }

    internal static string? GetValue(OdfNode root, string key)
    {
        foreach (OdfNode node in FindMatchingNodes(root, key))
        {
            if (IsTextInput(node))
            {
                return node.TextContent;
            }

            if (node.LocalName == "checkbox")
            {
                return node.GetAttribute("current-state", OdfNamespaces.Form) == "checked"
                    ? "true"
                    : "false";
            }

            return node.GetAttribute("value", OdfNamespaces.Form)
                ?? node.GetAttribute("current-value", OdfNamespaces.Form)
                ?? node.GetAttribute("label", OdfNamespaces.Form);
        }

        return null;
    }

    internal static bool TrySetValue(OdfNode root, string key, string? value)
    {
        bool updated = false;
        string normalized = value ?? string.Empty;
        foreach (OdfNode node in FindMatchingNodes(root, key))
        {
            if (IsTextInput(node))
            {
                node.TextContent = normalized;
                updated = true;
            }
            else if (node.LocalName == "checkbox")
            {
                node.SetAttribute("current-state", OdfNamespaces.Form, IsTruthy(normalized) ? "checked" : "unchecked", "form");
                updated = true;
            }
            else
            {
                node.SetAttribute("value", OdfNamespaces.Form, normalized, "form");
                updated = true;
            }
        }

        return updated;
    }

    private static IEnumerable<OdfNode> FindMatchingNodes(OdfNode root, string key)
    {
        if (key is null)
        {
            yield break;
        }

        foreach (OdfNode node in EnumerateElements(root))
        {
            if (IsTextInput(node) && IsTextInputMatch(node, key))
            {
                yield return node;
            }
            else if (node.NamespaceUri == OdfNamespaces.Form &&
                SupportedFormControls.Contains(node.LocalName) &&
                IsFormControlMatch(node, key))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<OdfNode> EnumerateElements(OdfNode node)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                yield return child;
                foreach (OdfNode descendant in EnumerateElements(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static bool IsTextInput(OdfNode node) =>
        node.NamespaceUri == OdfNamespaces.Text && node.LocalName == "text-input";

    private static bool IsTextInputMatch(OdfNode node, string key) =>
        string.Equals(node.GetAttribute("description", OdfNamespaces.Text), key, StringComparison.Ordinal) ||
        string.Equals(node.GetAttribute("name", OdfNamespaces.Text), key, StringComparison.Ordinal) ||
        string.Equals(node.TextContent, key, StringComparison.Ordinal);

    private static bool IsFormControlMatch(OdfNode node, string key) =>
        string.Equals(node.GetAttribute("name", OdfNamespaces.Form), key, StringComparison.Ordinal) ||
        string.Equals(node.GetAttribute("id", OdfNamespaces.Form), key, StringComparison.Ordinal);

    private static bool IsTruthy(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("checked", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("1", StringComparison.Ordinal);
}
