#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OdfKit.DOM;
using OdfKit.Core;
using OdfKit.Formula;

namespace OdfKit.Text
{
    public class OdfMailMergeEngine
    {
        private readonly TextDocument _doc;
        private readonly Dictionary<(Type, string), PropertyInfo?> _propertyCache = new();

        public OdfMailMergeEngine(TextDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public void Execute(OdfNode root, object dataSource)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (dataSource == null) return;

            ReplacePlaceholders(root, dataSource);
        }

        private void ReplacePlaceholders(OdfNode node, object dataSource)
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                if (text.Contains("{{") && text.Contains("}}"))
                {
                    node.TextContent = ReplaceTextWithDataSource(text, dataSource);
                }
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    string rowText = child.TextContent;
                    string? collectionName = FindCollectionName(rowText);
                    if (collectionName != null)
                    {
                        var collection = GetPropertyOrDictValue(dataSource, collectionName) as IEnumerable;
                        if (collection != null && !(collection is string))
                        {
                            var parent = child.Parent!;
                            int insertIdx = parent.Children.IndexOf(child);
                            parent.RemoveChild(child);

                            int cloneIndex = 0;
                            foreach (var item in collection)
                            {
                                var clonedRow = child.CloneNode(true);
                                
                                // To support both {{items.name}} and {{name}} inside the repeating row, 
                                // we can pass a composite resolver or clean the prefix.
                                // Let's support resolving with prefix or without prefix.
                                ReplacePlaceholdersInClonedRow(clonedRow, item, collectionName, dataSource);
                                
                                ShiftFormulasInRow(clonedRow, cloneIndex);
                                
                                parent.Children.Insert(insertIdx++, clonedRow);
                                clonedRow.Parent = parent;
                                cloneIndex++;
                            }
                            i += (insertIdx - 1 - i);
                            continue;
                        }
                    }
                }

                ReplacePlaceholders(child, dataSource);
            }
        }

        private void ReplacePlaceholdersInClonedRow(OdfNode node, object item, string prefix, object parentDataSource)
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                if (text.Contains("{{") && text.Contains("}}"))
                {
                    // Try to resolve both prefixed (e.g. {{items.name}}) and non-prefixed (e.g. {{name}})
                    int start = text.IndexOf("{{");
                    while (start != -1)
                    {
                        int end = text.IndexOf("}}", start);
                        if (end == -1) break;

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
                            Console.WriteLine($"DBGP: placeholder={placeholder} resolved={resolved} val={val}");
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
                ReplacePlaceholdersInClonedRow(child, item, prefix, parentDataSource);
            }
        }

        private void ShiftFormulasInRow(OdfNode node, int rowIndex)
        {
            if (node.NodeType == OdfNodeType.Element)
            {
                string? formula = node.GetAttribute("formula", OdfNamespaces.Table);
                if (formula != null)
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
                if (end == -1) break;
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
                if (end == -1) break;

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
            if (obj == null) return false;
            
            string[] parts = path.Split('.');
            object current = obj;
            
            foreach (var part in parts)
            {
                if (current == null) return false;
                
                var dict = current as IDictionary;
                if (dict != null)
                {
                    if (dict.Contains(part))
                    {
                        current = dict[part]!;
                    }
                    else
                    {
                        return false;
                    }
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

                    if (prop != null)
                    {
                        current = prop.GetValue(current)!;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            result = current;
            return true;
        }

        private object? GetPropertyOrDictValue(object obj, string key)
        {
            if (obj == null) return null;
            if (obj is IDictionary dict)
            {
                return dict.Contains(key) ? dict[key] : null;
            }
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
}
