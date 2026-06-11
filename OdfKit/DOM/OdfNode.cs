using System.Text;
using OdfKit.Core;

namespace OdfKit.DOM
{
    public enum OdfNodeType
    {
        Element,
        Text,
        Comment
    }

    public struct OdfAttributeName : IEquatable<OdfAttributeName>
    {
        public string LocalName { get; }
        public string NamespaceUri { get; }

        public OdfAttributeName(string localName, string namespaceUri)
        {
            LocalName = localName;
            NamespaceUri = namespaceUri;
        }

        public bool Equals(OdfAttributeName other) =>
            string.Equals(LocalName, other.LocalName, StringComparison.Ordinal) &&
            string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is OdfAttributeName name && Equals(name);

        public override int GetHashCode() =>
            (LocalName?.GetHashCode() ?? 0) ^ (NamespaceUri?.GetHashCode() ?? 0);
    }

    internal class OdfAttributeNameComparer : IEqualityComparer<OdfAttributeName>
    {
        public static readonly OdfAttributeNameComparer Instance = new();

        public bool Equals(OdfAttributeName x, OdfAttributeName y) => x.Equals(y);

        public int GetHashCode(OdfAttributeName obj) => obj.GetHashCode();
    }

    public class OdfNode
    {
        public OdfNodeType NodeType { get; }
        public string LocalName { get; }
        public string NamespaceUri { get; }
        public string? Prefix { get; set; }
        
        private string? _value; // For Text nodes
        public OdfNode? Parent { get; internal set; }
        public List<OdfNode> Children { get; } = new();
        public Dictionary<OdfAttributeName, string> Attributes { get; } = new(OdfAttributeNameComparer.Instance);
        
        /// <summary>
        /// 標記此節點是否被新增或修改（Dirty Flag），用於自動樣式去重。
        /// </summary>
        public bool IsModified { get; set; }

        public OdfNode(OdfNodeType nodeType, string localName, string namespaceUri, string? prefix = null)
        {
            NodeType = nodeType;
            LocalName = localName;
            NamespaceUri = namespaceUri;
            Prefix = prefix;
        }

        /// <summary>
        /// 遞迴重設此節點及其所有子節點的修改標記為 false。
        /// </summary>
        public void ResetModifiedState()
        {
            IsModified = false;
            foreach (var child in Children)
            {
                child.ResetModifiedState();
            }
        }

        /// <summary>
        /// 取得或設定節點內含的文字內容。
        /// 對於 Text 節點，這代表其直接值；對於 Element 節點，讀取會串接所有子 Text 節點，寫入會清除子節點並取代為單一 Text 節點。
        /// </summary>
        public virtual string TextContent
        {
            get
            {
                if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment)
                {
                    return _value ?? string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var child in Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        if (child.LocalName == "line-break")
                        {
                            sb.Append('\n');
                            continue;
                        }
                        if (child.LocalName == "tab")
                        {
                            sb.Append('\t');
                            continue;
                        }
                        if (child.LocalName == "s")
                        {
                            int count = 1;
                            string? cAttr = child.GetAttribute("c", OdfNamespaces.Text);
                            if (cAttr != null && int.TryParse(cAttr, out var parsedCount))
                            {
                                count = parsedCount;
                            }
                            sb.Append(' ', count);
                            continue;
                        }
                    }
                    sb.Append(child.TextContent);
                }
                return sb.ToString();
            }
            set
            {
                IsModified = true;
                if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment)
                {
                    _value = value;
                }
                else
                {
                    Children.Clear();
                    if (!string.IsNullOrEmpty(value))
                    {
                        var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
                        {
                            _value = value,
                            IsModified = true
                        };
                        AppendChild(textNode);
                    }
                }
            }
        }

        #region DOM Tree Manipulation

        public void AppendChild(OdfNode child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment)
            {
                throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
            }

            IsModified = true;
            child.Parent?.RemoveChild(child);
            child.Parent = this;
            Children.Add(child);
        }

        public void InsertBefore(OdfNode newChild, OdfNode refChild)
        {
            if (newChild == null) throw new ArgumentNullException(nameof(newChild));
            if (refChild == null) throw new ArgumentNullException(nameof(refChild));
            if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment)
            {
                throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
            }

            int index = Children.IndexOf(refChild);
            if (index == -1)
            {
                throw new InvalidOperationException("Reference node is not a child of this node.");
            }

            IsModified = true;
            newChild.Parent?.RemoveChild(newChild);
            newChild.Parent = this;
            Children.Insert(index, newChild);
        }

        public void InsertAfter(OdfNode newChild, OdfNode refChild)
        {
            if (newChild == null) throw new ArgumentNullException(nameof(newChild));
            if (refChild == null) throw new ArgumentNullException(nameof(refChild));
            if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment)
            {
                throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
            }

            int index = Children.IndexOf(refChild);
            if (index == -1)
            {
                throw new InvalidOperationException("Reference node is not a child of this node.");
            }

            IsModified = true;
            newChild.Parent?.RemoveChild(newChild);
            newChild.Parent = this;
            Children.Insert(index + 1, newChild);
        }

        public void RemoveChild(OdfNode child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (Children.Remove(child))
            {
                IsModified = true;
                child.Parent = null;
            }
        }

        #endregion

        #region Attributes Helper

        public string? GetAttribute(string localName, string namespaceUri)
        {
            var key = new OdfAttributeName(localName, namespaceUri);
            return Attributes.TryGetValue(key, out string? value) ? value : null;
        }

        public void SetAttribute(string localName, string namespaceUri, string value, string? prefix = null)
        {
            var key = new OdfAttributeName(localName, namespaceUri);
            if (!Attributes.TryGetValue(key, out string? existing) || existing != value)
            {
                IsModified = true;
                Attributes[key] = value;
            }
        }

        public void RemoveAttribute(string localName, string namespaceUri)
        {
            var key = new OdfAttributeName(localName, namespaceUri);
            if (Attributes.Remove(key))
            {
                IsModified = true;
            }
        }

        #endregion

        #region Clone & Import Node

        /// <summary>
        /// 複製當前節點。
        /// </summary>
        /// <param name="deep">是否進行深層複製（遞迴複製子節點）</param>
        public virtual OdfNode CloneNode(bool deep)
        {
            // Use custom Node Factory or reflection in higher layers. Here we clone as base OdfNode.
            var clone = new OdfNode(NodeType, LocalName, NamespaceUri, Prefix)
            {
                _value = _value
            };

            foreach (var attr in Attributes)
            {
                clone.Attributes[attr.Key] = attr.Value;
            }

            if (deep)
            {
                foreach (var child in Children)
                {
                    clone.AppendChild(child.CloneNode(true));
                }
            }

            return clone;
        }

        /// <summary>
        /// 將一個節點從來源 Package 匯入至目的 Package，自動複製並移轉其所屬的媒體檔案與樣式關聯。
        /// </summary>
        public static OdfNode ImportNode(OdfNode sourceNode, OdfPackage? sourcePackage, OdfPackage? destPackage)
        {
            if (sourceNode == null) throw new ArgumentNullException(nameof(sourceNode));

            // Deep clone the node structure first
            OdfNode importedNode = sourceNode.CloneNode(true);

            // If migrating between different packages, scan and rewrite media/picture assets
            if (sourcePackage != null && destPackage != null && sourcePackage != destPackage)
            {
                MigrateMediaReferences(importedNode, sourcePackage, destPackage);
            }

            return importedNode;
        }

        private static void MigrateMediaReferences(OdfNode node, OdfPackage sourcePackage, OdfPackage destPackage)
        {
            // Check xlink:href attribute in the node
            var hrefKey = new OdfAttributeName("href", OdfNamespaces.XLink);
            if (node.Attributes.TryGetValue(hrefKey, out string? href))
            {
                // Media references normally reside in "Pictures/" within the zip package
                if (href != null && href.StartsWith("Pictures/", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var stream = sourcePackage.GetEntryStream(href);
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        byte[] mediaBytes = ms.ToArray();

                        // Register media in the destination package
                        var mediaManager = new OdfMediaManager(destPackage);
                        string fileName = System.IO.Path.GetFileName(href);
                        string newHref = mediaManager.AddImage(mediaBytes, fileName);

                        // Update reference in cloned node
                        node.Attributes[hrefKey] = newHref;
                    }
                    catch (Exception ex)
                    {
                        OdfKitDiagnostics.Warn($"Failed to migrate media reference '{href}' during node import: {ex.Message}");
                    }
                }
            }

            // Recurse children
            foreach (var child in node.Children)
            {
                MigrateMediaReferences(child, sourcePackage, destPackage);
            }
        }

        #endregion
    }
}
