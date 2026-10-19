using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 保留 XML 區域命名空間宣告，包含僅在設定名稱等屬性值中使用的前綴。
/// </summary>
internal static class OdfNamespaceDeclarations
{
    internal const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
    internal const string OfficeConfigurationNamespace = "http://openoffice.org/2004/office";
    private static readonly ConditionalWeakTable<OdfNode, Dictionary<string, string>> Declarations = new();
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    internal static void Set(OdfNode node, Dictionary<string, string> declarations)
    {
        Declarations.Remove(node);
        Declarations.Add(node, declarations);
    }

    internal static IReadOnlyDictionary<string, string> GetOwn(OdfNode node)
    {
        Declarations.TryGetValue(node, out Dictionary<string, string>? declared);
        Dictionary<string, string>? result = null;
        foreach (KeyValuePair<OdfAttributeName, string> attribute in node.Attributes)
        {
            if (attribute.Key.NamespaceUri == XmlnsNamespace)
            {
                result ??= CopyDeclarations(declared ?? Empty);
                result[attribute.Key.LocalName] = attribute.Value;
            }
            else if (attribute.Key.NamespaceUri.Length == 0 && attribute.Key.LocalName == "xmlns")
            {
                result ??= CopyDeclarations(declared ?? Empty);
                result[string.Empty] = attribute.Value;
            }
        }
        return result ?? (IReadOnlyDictionary<string, string>?)declared ?? Empty;
    }

    internal static Dictionary<string, string> GetInScope(OdfNode node)
    {
        var path = new Stack<OdfNode>();
        for (OdfNode? current = node; current is not null; current = current.Parent)
            path.Push(current);
        var result = node._lazyInScopeNamespaces is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(node._lazyInScopeNamespaces, StringComparer.Ordinal);
        foreach (OdfNode current in path)
            foreach (KeyValuePair<string, string> declaration in GetOwn(current))
                result[declaration.Key] = declaration.Value;
        return result;
    }

    internal static string? ResolvePrefix(OdfNode node, string prefix)
    {
        if (prefix == "xml")
            return "http://www.w3.org/XML/1998/namespace";
        for (OdfNode? current = node; current is not null; current = current.Parent)
        {
            if (current.GetAttribute(prefix, XmlnsNamespace) is { } explicitValue)
                return explicitValue;
            if (Declarations.TryGetValue(current, out Dictionary<string, string>? declared) &&
                declared.TryGetValue(prefix, out string? value))
                return value;
        }
        return null;
    }

    internal static void BindOfficeSetting(OdfNode node)
    {
        string? name = node.GetAttribute("name", OdfNamespaces.Config);
        int separator = name?.IndexOf(':') ?? -1;
        if (separator <= 0)
            return;
        Dictionary<string, string> declarations = CopyDeclarations(GetOwn(node));
        declarations[name!.Substring(0, separator)] = OfficeConfigurationNamespace;
        Set(node, declarations);
    }

    internal static bool MatchesSettingName(OdfNode node, string name)
    {
        string? actual = node.GetAttribute("name", OdfNamespaces.Config);
        if (!name.StartsWith("ooo:", StringComparison.Ordinal))
            return actual == name;
        int separator = actual?.IndexOf(':') ?? -1;
        if (separator <= 0 || actual!.Substring(separator + 1) != name.Substring(4))
            return false;
        string? uri = ResolvePrefix(node, actual.Substring(0, separator));
        // 舊版 SDK 曾漏寫 ooo 宣告；保留讀取相容性，重新寫入時才補上明確宣告。
        return uri == OfficeConfigurationNamespace || uri is null && actual == name;
    }

    internal static void Copy(OdfNode source, OdfNode target, bool includeInherited)
    {
        IReadOnlyDictionary<string, string> declarations = includeInherited ? GetInScope(source) : GetOwn(source);
        if (declarations.Count > 0)
            Set(target, CopyDeclarations(declarations));
    }

    internal static void Forget(OdfNode node) => Declarations.Remove(node);

    private static Dictionary<string, string> CopyDeclarations(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> declaration in source)
            result.Add(declaration.Key, declaration.Value);
        return result;
    }
}
