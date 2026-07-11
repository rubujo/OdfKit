using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    /// <summary>
    /// Gets a summary list of all user field (template variable) declarations in the document.
    /// 取得文件中所有使用者欄位（範本變數）宣告的摘要清單。
    /// </summary>
    /// <returns>The user field declaration summary list. / 使用者欄位宣告摘要清單。</returns>
    public IReadOnlyList<OdfUserFieldDeclarationInfo> GetUserFieldDeclarations()
    {
        List<OdfUserFieldDeclarationInfo> results = [];
        OdfNode? decls = BodyTextRoot.FindChildElement("user-field-decls", OdfNamespaces.Text);
        if (decls is null)
        {
            return results;
        }

        foreach (OdfNode child in decls.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "user-field-decl" ||
                child.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            string name = child.GetAttribute("name", OdfNamespaces.Text) ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            string valueType = child.GetAttribute("value-type", OdfNamespaces.Office) ?? "string";
            results.Add(new OdfUserFieldDeclarationInfo(name, valueType, ReadUserFieldValue(child, valueType)));
        }

        return results;
    }

    /// <summary>
    /// Finds a user-field declaration by its exact name.
    /// 依精確名稱尋找使用者欄位宣告。
    /// </summary>
    /// <param name="name">The exact field name. / 精確的欄位名稱。</param>
    /// <returns>The matching declaration, or <see langword="null"/>. / 相符的宣告；若不存在則為 <see langword="null"/>。</returns>
    public OdfUserFieldDeclarationInfo? FindUserFieldDeclaration(string name)
    {
        foreach (OdfUserFieldDeclarationInfo declaration in GetUserFieldDeclarations())
        {
            if (string.Equals(declaration.Name, name, StringComparison.Ordinal))
                return declaration;
        }
        return null;
    }

    /// <summary>
    /// Adds or updates a user field (template variable) declaration.
    /// 新增或更新一個使用者欄位（範本變數）宣告。
    /// </summary>
    /// <param name="name">The field name. / 欄位名稱。</param>
    /// <param name="valueType">The value type (e.g. <c>string</c>, <c>float</c>, <c>boolean</c>, <c>date</c>, <c>time</c>). / 值類型（例如 <c>string</c>、<c>float</c>、<c>boolean</c>、<c>date</c>、<c>time</c>）。</param>
    /// <param name="value">The raw text of the field's value. / 欄位的值原文。</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="valueType"/> is blank. / 當 <paramref name="name"/> 或 <paramref name="valueType"/> 為空白時擲出。</exception>
    public void AddUserFieldDeclaration(string name, string valueType, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_UserCannotBeEmpty_3"), nameof(name));
        }

        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_UserCannotBeEmpty_2"), nameof(valueType));
        }

        OdfNode decls = FindOrCreateUserFieldDecls();
        OdfNode? decl = FindUserFieldDecl(decls, name);
        if (decl is null)
        {
            decl = new OdfNode(OdfNodeType.Element, "user-field-decl", OdfNamespaces.Text, "text");
            decl.SetAttribute("name", OdfNamespaces.Text, name, "text");
            decls.AppendChild(decl);
        }

        decl.SetAttribute("value-type", OdfNamespaces.Office, valueType, "office");
        WriteUserFieldValue(decl, valueType, value ?? string.Empty);
    }

    /// <summary>
    /// Sets the value of an existing user field (template variable) declaration.
    /// 設定既有使用者欄位（範本變數）宣告的值。
    /// </summary>
    /// <param name="name">The field name. / 欄位名稱。</param>
    /// <param name="value">The raw text of the value to set. / 要設定的值原文。</param>
    /// <returns><see langword="true"/> if set successfully; <see langword="false"/> if no declaration with the given name was found. / 若成功設定則為 <see langword="true"/>；找不到對應名稱的欄位宣告時為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is blank. / 當 <paramref name="name"/> 為空白時擲出。</exception>
    public bool SetUserFieldValue(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_UserCannotBeEmpty_3"), nameof(name));
        }

        OdfNode? decls = BodyTextRoot.FindChildElement("user-field-decls", OdfNamespaces.Text);
        OdfNode? decl = decls is null ? null : FindUserFieldDecl(decls, name);
        if (decl is null)
        {
            return false;
        }

        string valueType = decl.GetAttribute("value-type", OdfNamespaces.Office) ?? "string";
        WriteUserFieldValue(decl, valueType, value ?? string.Empty);
        return true;
    }

    /// <summary>
    /// Removes a user-field declaration by name.
    /// 依名稱移除使用者欄位宣告。
    /// </summary>
    /// <param name="name">The exact field name. / 精確的欄位名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveUserFieldDeclaration(string name)
    {
        OdfNode? declarations = BodyTextRoot.FindChildElement("user-field-decls", OdfNamespaces.Text);
        OdfNode? declaration = declarations is null ? null : FindUserFieldDecl(declarations, name);
        if (declaration is null || declarations is null)
            return false;
        declarations.RemoveChild(declaration);
        if (declarations.Children.Count == 0)
            BodyTextRoot.RemoveChild(declarations);
        return true;
    }

    /// <summary>
    /// Removes all user-field declarations while preserving unknown declaration-container content.
    /// 移除所有使用者欄位宣告，同時保留宣告容器中的未知內容。
    /// </summary>
    /// <returns>The number of removed declarations. / 已移除的宣告數量。</returns>
    public int ClearUserFieldDeclarations()
    {
        OdfNode? declarations = BodyTextRoot.FindChildElement("user-field-decls", OdfNamespaces.Text);
        if (declarations is null)
            return 0;
        int removed = 0;
        foreach (OdfNode child in new List<OdfNode>(declarations.Children))
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "user-field-decl" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                declarations.RemoveChild(child))
            {
                removed++;
            }
        }
        if (declarations.Children.Count == 0)
            BodyTextRoot.RemoveChild(declarations);
        return removed;
    }

    private OdfNode FindOrCreateUserFieldDecls()
    {
        OdfNode? existing = BodyTextRoot.FindChildElement("user-field-decls", OdfNamespaces.Text);
        if (existing is not null)
        {
            return existing;
        }

        OdfNode decls = new(OdfNodeType.Element, "user-field-decls", OdfNamespaces.Text, "text");
        if (BodyTextRoot.Children.Count > 0)
        {
            BodyTextRoot.InsertBefore(decls, BodyTextRoot.Children[0]);
        }
        else
        {
            BodyTextRoot.AppendChild(decls);
        }

        return decls;
    }

    private static OdfNode? FindUserFieldDecl(OdfNode decls, string name)
    {
        foreach (OdfNode child in decls.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "user-field-decl" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Text), name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static string? ReadUserFieldValue(OdfNode decl, string valueType)
    {
        return valueType switch
        {
            "float" or "percentage" or "currency" => decl.GetAttribute("value", OdfNamespaces.Office),
            "boolean" => decl.GetAttribute("boolean-value", OdfNamespaces.Office),
            "date" => decl.GetAttribute("date-value", OdfNamespaces.Office),
            "time" => decl.GetAttribute("time-value", OdfNamespaces.Office),
            _ => decl.GetAttribute("string-value", OdfNamespaces.Office),
        };
    }

    private static void WriteUserFieldValue(OdfNode decl, string valueType, string value)
    {
        switch (valueType)
        {
            case "float":
            case "percentage":
            case "currency":
                decl.SetAttribute("value", OdfNamespaces.Office, value, "office");
                break;
            case "boolean":
                decl.SetAttribute("boolean-value", OdfNamespaces.Office, value, "office");
                break;
            case "date":
                decl.SetAttribute("date-value", OdfNamespaces.Office, value, "office");
                break;
            case "time":
                decl.SetAttribute("time-value", OdfNamespaces.Office, value, "office");
                break;
            default:
                decl.SetAttribute("string-value", OdfNamespaces.Office, value, "office");
                break;
        }
    }
}
