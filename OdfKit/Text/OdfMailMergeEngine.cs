using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件的郵件合併引擎。
/// </summary>
/// <param name="doc">目標文字文件</param>
public class OdfMailMergeEngine(TextDocument doc)
{
    private readonly TextDocument _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    private readonly Dictionary<(Type, string), PropertyInfo?> _propertyCache = [];

    /// <summary>
    /// 執行郵件合併作業。
    /// </summary>
    /// <param name="root">郵件合併的根節點</param>
    /// <param name="dataSource">用來合併的資料來源物件</param>
    public void Execute(OdfNode root, object dataSource)
    {
        Execute(root, dataSource, null);
    }

    /// <summary>
    /// 執行郵件合併作業並傳回報告，支援 options 設定。
    /// </summary>
    /// <param name="root">郵件合併的根節點</param>
    /// <param name="dataSource">用來合併的資料來源物件</param>
    /// <param name="options">郵件合併選項，若為 null 則使用預設值。</param>
    /// <returns>郵件合併執行報告。</returns>
    public OdfMailMergeReport Execute(OdfNode root, object dataSource, OdfMailMergeOptions? options)
    {
        if (root is null)
            throw new ArgumentNullException(nameof(root));

        options ??= new OdfMailMergeOptions();
        var report = new OdfMailMergeReport();

        if (dataSource is null)
            return report;

        // 1. 遞迴處理 TableStart / TableEnd 區域展開
        ProcessRegions(root, dataSource, options, 0, null, report);

        // 2. 替換剩餘的一般預留位置
        ReplacePlaceholdersWithReport(root, dataSource, null, report);

        return report;
    }

    private void ProcessRegions(OdfNode parent, object dataSource, OdfMailMergeOptions options, int depth, object? parentDataSource, OdfMailMergeReport report)
    {
        if (depth > options.MaxNestingDepth)
            return;

        // 兄弟節點掃描與區域展開
        for (int i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            string content = child.TextContent;
            int startTokenIdx = content.IndexOf(options.RegionStartToken);
            if (startTokenIdx != -1)
            {
                int endTokenIdx = content.IndexOf("}}", startTokenIdx);
                if (endTokenIdx != -1)
                {
                    string regionName = content.Substring(startTokenIdx + options.RegionStartToken.Length, endTokenIdx - (startTokenIdx + options.RegionStartToken.Length)).Trim();
                    int startIndex = i;
                    int endIndex = -1;

                    // 尋找對應的 TableEnd 兄弟節點
                    string endToken = options.RegionEndToken + regionName + "}}";
                    for (int j = startIndex + 1; j < parent.Children.Count; j++)
                    {
                        if (parent.Children[j].TextContent.Contains(endToken))
                        {
                            endIndex = j;
                            break;
                        }
                    }

                    if (endIndex != -1)
                    {
                        // 收集模板節點
                        var templateNodes = new List<OdfNode>();
                        for (int k = startIndex + 1; k < endIndex; k++)
                        {
                            templateNodes.Add(parent.Children[k]);
                        }

                        // 取得資料來源中該 regionName 的資料
                        bool resolved = TryResolveValuePath(dataSource, regionName, out object? val);
                        if (!resolved && parentDataSource is not null)
                        {
                            resolved = TryResolveValuePath(parentDataSource, regionName, out val);
                        }

                        var collection = val as IEnumerable;
                        if (resolved && collection is not null && collection is not string)
                        {
                            // 先從 parent 移除起點、模板與終點節點
                            parent.RemoveChild(parent.Children[startIndex]);
                            for (int k = 0; k < templateNodes.Count + 1; k++)
                            {
                                parent.RemoveChild(parent.Children[startIndex]);
                            }

                            int currentInsertIdx = startIndex;
                            foreach (var item in collection)
                            {
                                var clonedNodes = new List<OdfNode>();
                                foreach (var tNode in templateNodes)
                                {
                                    clonedNodes.Add(tNode.CloneNode(true));
                                }

                                // 建立一個虛擬的臨時父節點來包裝 clonedNodes，以利內層兄弟區段嵌套展開
                                var tempParent = OdfNodeFactory.CreateElement("temp-container", OdfNamespaces.Text, "text");
                                foreach (var clonedNode in clonedNodes)
                                {
                                    tempParent.AppendChild(clonedNode);
                                }

                                // 對臨時父節點進行內層的遞迴展開與置換
                                ProcessRegions(tempParent, item, options, depth + 1, dataSource, report);
                                ReplacePlaceholdersWithReport(tempParent, item, dataSource, report);

                                // 將展開後的所有子節點移回真正的 parent 中
                                var finalNodes = tempParent.Children.ToList();
                                foreach (var finalNode in finalNodes)
                                {
                                    tempParent.RemoveChild(finalNode);
                                    parent.Children.Insert(currentInsertIdx++, finalNode);
                                    finalNode.Parent = parent;
                                }
                            }

                            // 重設 i 到最新插入位置的後一個節點
                            i = currentInsertIdx - 1;
                            continue;
                        }
                        else
                        {
                            // 找不到資料或資料無效，直接清空該區域，並記錄為未解析
                            report.UnresolvedPlaceholders.Add(regionName);

                            parent.RemoveChild(parent.Children[startIndex]);
                            for (int k = 0; k < templateNodes.Count + 1; k++)
                            {
                                parent.RemoveChild(parent.Children[startIndex]);
                            }

                            i = startIndex - 1;
                            continue;
                        }
                    }
                }
            }

            // 遞迴子節點
            ProcessRegions(child, dataSource, options, depth, parentDataSource, report);
        }
    }

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
}
