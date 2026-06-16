using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;

namespace OdfKit.Text;

public partial class OdfMailMergeEngine
{
    #region Placeholder Replacement

    private void ReplacePlaceholdersWithReport(OdfNode node, object dataSource, object? parentDataSource, OdfMailMergeReport report)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (text.Contains("{{") && text.Contains("}}"))
            {
                int start = text.IndexOf("{{");
                while (start != -1)
                {
                    int end = text.IndexOf("}}", start);
                    if (end == -1)
                        break;

                    string placeholder = text.Substring(start + 2, end - start - 2).Trim();

                    // 忽略 TableStart / TableEnd 的 placeholder，因為它們在 ProcessRegions 已經被處理了
                    if (placeholder.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase) ||
                        placeholder.StartsWith("TableEnd:", StringComparison.OrdinalIgnoreCase))
                    {
                        start = text.IndexOf("{{", end);
                        continue;
                    }

                    object? val = null;
                    bool resolved = TryResolveValuePath(dataSource, placeholder, out val);
                    if (!resolved && parentDataSource is not null)
                    {
                        resolved = TryResolveValuePath(parentDataSource, placeholder, out val);
                    }

                    if (!resolved)
                    {
                        report.UnresolvedPlaceholders.Add(placeholder);
                    }

                    string valStr = val?.ToString() ?? string.Empty;
                    text = text.Substring(0, start) + valStr + text.Substring(end + 2);
                    start = text.IndexOf("{{", start + valStr.Length);
                }
                node.TextContent = text;
            }
            return;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];

            // 既有 row 集合展開向下相容
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                string rowText = child.TextContent;
                if (!rowText.Contains("{{TableStart:") && !rowText.Contains("{{TableEnd:"))
                {
                    string? collectionName = FindCollectionName(rowText);
                    if (collectionName is not null)
                    {
                        bool resolved = TryResolveValuePath(dataSource, collectionName, out object? val);
                        if (!resolved && parentDataSource is not null)
                        {
                            resolved = TryResolveValuePath(parentDataSource, collectionName, out val);
                        }

                        var collection = val as IEnumerable;
                        if (resolved && collection is not null && collection is not string)
                        {
                            var parentNode = child.Parent!;
                            int insertIdx = parentNode.Children.IndexOf(child);
                            parentNode.RemoveChild(child);

                            int cloneIndex = 0;
                            foreach (var item in collection)
                            {
                                var clonedRow = child.CloneNode(true);
                                ReplacePlaceholdersInClonedRowWithReport(clonedRow, item, collectionName, dataSource, report);
                                ShiftFormulasInRow(clonedRow, cloneIndex);

                                parentNode.Children.Insert(insertIdx++, clonedRow);
                                clonedRow.Parent = parentNode;
                                cloneIndex++;
                            }
                            i += (insertIdx - 1 - i);
                            continue;
                        }
                        else
                        {
                            report.UnresolvedPlaceholders.Add(collectionName);
                        }
                    }
                }
            }

            ReplacePlaceholdersWithReport(child, dataSource, parentDataSource, report);
        }
    }

    private void ReplacePlaceholdersInClonedRowWithReport(OdfNode node, object item, string prefix, object parentDataSource, OdfMailMergeReport report)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (text.Contains("{{") && text.Contains("}}"))
            {
                int start = text.IndexOf("{{");
                while (start != -1)
                {
                    int end = text.IndexOf("}}", start);
                    if (end == -1)
                        break;

                    string placeholder = text.Substring(start + 2, end - start - 2).Trim();
                    object? val = null;
                    bool resolved = false;

                    if (placeholder.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        string subPath = placeholder.Substring(prefix.Length + 1);
                        resolved = TryResolveValuePath(item, subPath, out val);
                    }
                    else
                    {
                        resolved = TryResolveValuePath(item, placeholder, out val);
                    }

                    if (!resolved)
                    {
                        resolved = TryResolveValuePath(parentDataSource, placeholder, out val);
                    }

                    if (!resolved)
                    {
                        report.UnresolvedPlaceholders.Add(placeholder);
                    }

                    string valStr = val?.ToString() ?? string.Empty;
                    text = text.Substring(0, start) + valStr + text.Substring(end + 2);
                    start = text.IndexOf("{{", start + valStr.Length);
                }
                node.TextContent = text;
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ReplacePlaceholdersInClonedRowWithReport(child, item, prefix, parentDataSource, report);
        }
    }

    private void ShiftFormulasInRow(OdfNode node, int rowIndex)
    {
        if (node.NodeType == OdfNodeType.Element)
        {
            string? formula = node.GetAttribute("formula", OdfNamespaces.Table);
            if (formula is not null)
            {
                string translated = OdfFormulaTranslator.TranslateFormulaOffset(formula, rowIndex, 0);
                node.SetAttribute("formula", OdfNamespaces.Table, translated);
            }
        }

        foreach (var child in node.Children)
        {
            ShiftFormulasInRow(child, rowIndex);
        }
    }

    private string? FindCollectionName(string text)
    {
        int start = text.IndexOf("{{");
        while (start != -1)
        {
            int end = text.IndexOf("}}", start);
            if (end == -1)
                break;
            string path = text.Substring(start + 2, end - start - 2).Trim();
            int dotIdx = path.IndexOf('.');
            if (dotIdx > 0)
            {
                return path.Substring(0, dotIdx);
            }
            start = text.IndexOf("{{", end);
        }
        return null;
    }

    private string ReplaceTextWithDataSource(string text, object dataSource)
    {
        int start = text.IndexOf("{{");
        while (start != -1)
        {
            int end = text.IndexOf("}}", start);
            if (end == -1)
                break;

            string placeholder = text.Substring(start + 2, end - start - 2).Trim();
            TryResolveValuePath(dataSource, placeholder, out object? val);
            string valStr = val?.ToString() ?? string.Empty;

            text = text.Substring(0, start) + valStr + text.Substring(end + 2);
            start = text.IndexOf("{{", start + valStr.Length);
        }
        return text;
    }

    private bool TryResolveValuePath(object obj, string path, out object? result)
    {
        result = null;
        if (obj is null)
            return false;

        string[] parts = path.Split('.');
        object current = obj;

        foreach (var part in parts)
        {
            if (current is null)
                return false;

            if (current is IDictionary dict)
            {
                if (dict.Contains(part))
                    current = dict[part]!;
                else
                    return false;
            }
            else if (current is IReadOnlyDictionary<string, object?> roDict)
            {
                if (roDict.TryGetValue(part, out var val))
                    current = val!;
                else
                    return false;
            }
            else
            {
                var type = current.GetType();
                var key = (type, part);
                if (!_propertyCache.TryGetValue(key, out var prop))
                {
                    prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    _propertyCache[key] = prop;
                }

                if (prop is not null)
                    current = prop.GetValue(current)!;
                else
                    return false;
            }
        }
        result = current;
        return true;
    }

    private object? GetPropertyOrDictValue(object obj, string key)
    {
        if (obj is null)
            return null;
        if (obj is IDictionary dict)
            return dict.Contains(key) ? dict[key] : null;
        if (obj is IReadOnlyDictionary<string, object?> roDict)
            return roDict.TryGetValue(key, out var val) ? val : null;
        var type = obj.GetType();
        var cacheKey = (type, key);
        if (!_propertyCache.TryGetValue(cacheKey, out var prop))
        {
            prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            _propertyCache[cacheKey] = prop;
        }
        return prop?.GetValue(obj);
    }

    #endregion
}
