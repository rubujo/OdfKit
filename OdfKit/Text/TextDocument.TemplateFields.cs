using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Text;

public partial class TextDocument
{
    /// <summary>
    /// Gets get user field declarations.
    /// 取得文件中所有使用者欄位（範本變數）宣告的摘要清單。
    /// </summary>
    /// <returns>The result. / 使用者欄位宣告摘要清單</returns>
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
    /// Adds add user field declaration.
    /// 新增或更新一個使用者欄位（範本變數）宣告。
    /// </summary>
    /// <param name="name">The name or identifier. / 欄位名稱</param>
    /// <param name="valueType">The text or value. / 值類型（例如 <c>string</c>、<c>float</c>、<c>boolean</c>、<c>date</c>、<c>time</c>）</param>
    /// <param name="value">The text or value. / 欄位的值原文</param>
    /// <exception cref="ArgumentException">Thrown when the documented condition occurs. / 當 <paramref name="name"/> 或 <paramref name="valueType"/> 為空白時擲出</exception>
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
    /// Sets set user field value.
    /// 設定既有使用者欄位（範本變數）宣告的值。
    /// </summary>
    /// <param name="name">The name or identifier. / 欄位名稱</param>
    /// <param name="value">The text or value. / 要設定的值原文</param>
    /// <returns>The result. / 若成功設定則為 <see langword="true"/>；找不到對應名稱的欄位宣告時為 <see langword="false"/></returns>
    /// <exception cref="ArgumentException">Thrown when the documented condition occurs. / 當 <paramref name="name"/> 為空白時擲出</exception>
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
