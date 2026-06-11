using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core
{
    public abstract class OdfDocument : IDisposable, IAsyncDisposable
    {
        public OdfPackage Package { get; }
        public OdfNode ContentDom { get; protected set; } = null!;
        public OdfNode StylesDom { get; protected set; } = null!;
        public OdfStyleEngine StyleEngine { get; protected set; } = null!;
        public OdfNode MetaDom { get; protected set; } = null!;
        public OdfNode SettingsDom { get; protected set; } = null!;

        public OdfNode ContentRoot { get => ContentDom; protected set => ContentDom = value; }
        public OdfNode StylesRoot { get => StylesDom; protected set => StylesDom = value; }
        public OdfNode MetaRoot { get => MetaDom; protected set => MetaDom = value; }
        public OdfNode SettingsRoot { get => SettingsDom; protected set => SettingsDom = value; }

        private bool _isDisposed;

        protected OdfDocument(OdfPackage package)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            
            LoadXmlTrees();
            StyleEngine = new OdfStyleEngine(ContentDom, StylesDom);
        }

        /// <summary>
        /// Sanitizes the document by removing all VBA/StarBasic scripts, signatures, and script references.
        /// </summary>
        public void SanitizeMacros()
        {
            OdfPackage.SanitizeXmlNode(ContentDom);
            OdfPackage.SanitizeXmlNode(StylesDom);
            OdfPackage.SanitizeXmlNode(MetaDom);
            OdfPackage.SanitizeXmlNode(SettingsDom);
            Package.SanitizeMacros();
        }

        private void LoadXmlTrees()
        {
            ContentDom = LoadOrInitDom("content.xml", GetDefaultContentXml());
            StylesDom = LoadOrInitDom("styles.xml", GetDefaultStylesXml());
            MetaDom = LoadOrInitDom("meta.xml", GetDefaultMetaXml());
            SettingsDom = LoadOrInitDom("settings.xml", GetDefaultSettingsXml());
        }

        private OdfNode LoadOrInitDom(string entryName, string defaultXml)
        {
            if (Package.HasEntry(entryName))
            {
                using var stream = Package.GetEntryStream(entryName);
                return OdfXmlReader.Parse(stream);
            }
            else
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(defaultXml));
                return OdfXmlReader.Parse(ms);
            }
        }

        protected abstract string GetDefaultContentXml();
        protected abstract string GetDefaultStylesXml();

        protected virtual string GetDefaultMetaXml()
        {
            return "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.3\"><office:meta></office:meta></office:document-meta>";
        }

        protected virtual string GetDefaultSettingsXml()
        {
            return "<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:settings></office:settings></office:document-settings>";
        }

        #region Package Lifecycle & Persistence

        public virtual void Save(OdfSaveOptions? options = null)
        {
            options ??= OdfSaveOptions.Default;

            StyleEngine.DeduplicateAndSaveStyles();
            UpdateDocumentStatistics();

            WriteDomToEntry("content.xml", ContentDom, options);
            WriteDomToEntry("styles.xml", StylesDom, options);
            WriteDomToEntry("meta.xml", MetaDom, options);
            WriteDomToEntry("settings.xml", SettingsDom, options);

            Package.Save();
        }

        public virtual async Task SaveAsync(OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= OdfSaveOptions.Default;

            StyleEngine.DeduplicateAndSaveStyles();
            UpdateDocumentStatistics();

            WriteDomToEntry("content.xml", ContentDom, options);
            WriteDomToEntry("styles.xml", StylesDom, options);
            WriteDomToEntry("meta.xml", MetaDom, options);
            WriteDomToEntry("settings.xml", SettingsDom, options);

            await Package.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        private void WriteDomToEntry(string name, OdfNode node, OdfSaveOptions options)
        {
            using var ms = new MemoryStream();
            OdfXmlWriter.Write(node, ms, options);
            Package.WriteEntry(name, ms.ToArray(), "text/xml");
        }

        #endregion

        #region High-Level Digital Signatures

        public void Sign(X509Certificate2 certificate)
        {
            StyleEngine.DeduplicateAndSaveStyles();
            WriteDomToEntry("content.xml", ContentDom, OdfSaveOptions.Default);
            WriteDomToEntry("styles.xml", StylesDom, OdfSaveOptions.Default);
            WriteDomToEntry("meta.xml", MetaDom, OdfSaveOptions.Default);
            WriteDomToEntry("settings.xml", SettingsDom, OdfSaveOptions.Default);

            OdfSigner.Sign(Package, certificate);
        }

        public bool VerifySignatures(out X509Certificate2Collection certificates)
        {
            return OdfSigner.VerifySignatures(Package, out certificates);
        }

        #endregion

        #region Metadata API (meta.xml)

        public string? Title
        {
            get => GetMetaElementText("dc:title");
            set => SetMetaElementText("dc:title", value);
        }

        public string? Creator
        {
            get => GetMetaElementText("dc:creator");
            set => SetMetaElementText("dc:creator", value);
        }

        public string? Description
        {
            get => GetMetaElementText("dc:description");
            set => SetMetaElementText("dc:description", value);
        }

        public string? Subject
        {
            get => GetMetaElementText("dc:subject");
            set => SetMetaElementText("dc:subject", value);
        }

        public string? Language
        {
            get => GetMetaElementText("dc:language");
            set => SetMetaElementText("dc:language", value);
        }

        public DateTime? CreationDate
        {
            get => ParseMetaDate(GetMetaElementText("meta:creation-date"));
            set => SetMetaElementText("meta:creation-date", FormatMetaDate(value));
        }

        public DateTime? ModificationDate
        {
            get => ParseMetaDate(GetMetaElementText("dc:date"));
            set => SetMetaElementText("dc:date", FormatMetaDate(value));
        }

        public void SetCustomProperty(string name, object value, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property name cannot be empty.", nameof(name));
            
            if (name.Contains(":"))
            {
                string oldName = name;
                name = name.Replace(":", "_");
                OdfKitDiagnostics.Warn($"Custom property name '{oldName}' contains invalid character ':'. Renamed to '{name}' for Excel compatibility.");
            }

            var metaRoot = FindOrCreateMetaRoot();
            
            OdfNode? existing = FindCustomPropertyNode(metaRoot, name);
            if (existing != null) metaRoot.RemoveChild(existing);

            var propNode = new OdfNode(OdfNodeType.Element, "user-defined", OdfNamespaces.Meta, "meta");
            propNode.SetAttribute("name", OdfNamespaces.Meta, name, "meta");
            propNode.SetAttribute("value-type", OdfNamespaces.Meta, type, "meta");
            propNode.TextContent = FormatValue(value, type);

            metaRoot.AppendChild(propNode);
        }

        public object? GetCustomProperty(string name)
        {
            var metaRoot = FindOrCreateMetaRoot();
            var propNode = FindCustomPropertyNode(metaRoot, name);
            if (propNode == null) return null;

            string? type = propNode.GetAttribute("value-type", OdfNamespaces.Meta);
            string valStr = propNode.TextContent;
            return ParseValue(valStr, type);
        }

        #endregion

        #region Zoom & View Settings (settings.xml)

        public double ZoomLevel
        {
            get => GetZoomLevelInternal();
            set => SetZoomLevelInternal(value);
        }

        #endregion

        #region Web Streaming APIs

        public byte[] SaveToBytes()
        {
            using var ms = new MemoryStream();
            SaveToStream(ms);
            return ms.ToArray();
        }

        public void SaveToStream(Stream destinationStream)
        {
            if (destinationStream == null) throw new ArgumentNullException(nameof(destinationStream));

            StyleEngine.DeduplicateAndSaveStyles();
            UpdateDocumentStatistics();

            WriteDomToEntry("content.xml", ContentDom, OdfSaveOptions.Default);
            WriteDomToEntry("styles.xml", StylesDom, OdfSaveOptions.Default);
            WriteDomToEntry("meta.xml", MetaDom, OdfSaveOptions.Default);
            WriteDomToEntry("settings.xml", SettingsDom, OdfSaveOptions.Default);

            Package.SaveToStream(destinationStream);
            
            if (destinationStream.CanSeek)
            {
                destinationStream.Position = 0;
            }
        }

        #endregion

        #region Document Merging API

        public virtual void AppendDocument(OdfDocument otherDoc, OdfMergeOptions? options = null)
        {
            options ??= OdfMergeOptions.Default;
            if (otherDoc == null) throw new ArgumentNullException(nameof(otherDoc));

            var styleRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

            if (options.ImportStyles)
            {
                MergeStyles(otherDoc, options, styleRenameMap);
            }

            MergeContentNodes(otherDoc, options, styleRenameMap);
        }

        #endregion

        #region Helper Methods

        protected OdfNode FindOrCreateMetaRoot()
        {
            foreach (var child in MetaDom.Children)
            {
                if (child.LocalName == "meta" && child.NamespaceUri == OdfNamespaces.Office)
                    return child;
            }
            var root = new OdfNode(OdfNodeType.Element, "meta", OdfNamespaces.Office, "office");
            MetaDom.AppendChild(root);
            return root;
        }

        private OdfNode? FindCustomPropertyNode(OdfNode metaRoot, string name)
        {
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == "user-defined" && 
                    child.NamespaceUri == OdfNamespaces.Meta && 
                    child.GetAttribute("name", OdfNamespaces.Meta) == name)
                {
                    return child;
                }
            }
            return null;
        }

        private string? GetMetaElementText(string qualifiedName)
        {
            var metaRoot = FindOrCreateMetaRoot();
            string localName = qualifiedName.Split(':')[1];
            string ns = qualifiedName.StartsWith("dc:") ? OdfNamespaces.Dc : OdfNamespaces.Meta;

            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child.TextContent;
            }
            return null;
        }

        private void SetMetaElementText(string qualifiedName, string? value)
        {
            var metaRoot = FindOrCreateMetaRoot();
            string[] parts = qualifiedName.Split(':');
            string localName = parts[1];
            string ns = parts[0] == "dc" ? OdfNamespaces.Dc : OdfNamespaces.Meta;
            string prefix = parts[0];

            OdfNode? target = null;
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                {
                    target = child;
                    break;
                }
            }

            if (value == null)
            {
                if (target != null) metaRoot.RemoveChild(target);
            }
            else
            {
                if (target == null)
                {
                    target = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
                    metaRoot.AppendChild(target);
                }
                target.TextContent = value;
            }
        }

        private DateTime? ParseMetaDate(string? text)
        {
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var val))
            {
                if (val == DateTime.MinValue || val == DateTime.MaxValue)
                    return val;
                try
                {
                    return val.ToUniversalTime();
                }
                catch (ArgumentOutOfRangeException)
                {
                    return val;
                }
            }
            return null;
        }

        private string? FormatMetaDate(DateTime? dt)
        {
            if (dt == null) return null;
            var val = dt.Value;
            if (val == DateTime.MinValue || val == DateTime.MaxValue)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            try
            {
                return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private string FormatValue(object val, string type)
        {
            return type.ToLowerInvariant() switch
            {
                "boolean" => ((bool)val) ? "true" : "false",
                "float" => Convert.ToDouble(val).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "date" => FormatDateValue((DateTime)val),
                _ => val.ToString() ?? string.Empty
            };
        }

        private string FormatDateValue(DateTime val)
        {
            if (val == DateTime.MinValue || val == DateTime.MaxValue)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            try
            {
                return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private object ParseValue(string val, string? type)
        {
            return type?.ToLowerInvariant() switch
            {
                "boolean" => bool.TryParse(val, out var b) && b,
                "float" => double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0.0,
                "date" => DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue,
                _ => val
            };
        }

        private double GetZoomLevelInternal()
        {
            var entry = FindSettingsConfigItem("ZoomValue");
            if (entry != null && double.TryParse(entry.TextContent, out var val))
                return val;
            return 100.0;
        }

        private void SetZoomLevelInternal(double zoom)
        {
            var settingsRoot = SettingsDom;
            var setNode = FindOrCreateSettingsNode(settingsRoot, "view-settings");
            var mapNode = FindOrCreateMapNode(setNode, "Views");
            var entryNode = FindOrCreateMapEntryNode(mapNode);
            var zoomNode = FindOrCreateConfigItemNode(entryNode, "ZoomValue", "int");
            zoomNode.TextContent = Math.Round(zoom).ToString();
            
            var zoomTypeNode = FindOrCreateConfigItemNode(entryNode, "ZoomType", "short");
            zoomTypeNode.TextContent = "0"; // 0: Direct Zoom percentage
        }

        protected OdfNode? FindSettingsConfigItem(string name)
        {
            return FindNodeByNameRecursive(SettingsDom, "config-item", name);
        }

        private OdfNode? FindNodeByNameRecursive(OdfNode parent, string localName, string nameAttr)
        {
            if (parent.LocalName == localName && parent.GetAttribute("name", OdfNamespaces.Config) == nameAttr)
                return parent;
            foreach (var child in parent.Children)
            {
                var f = FindNodeByNameRecursive(child, localName, nameAttr);
                if (f != null) return f;
            }
            return null;
        }

        protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
        {
            foreach (var child in root.Children)
            {
                if (child.LocalName == "settings" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    foreach (var sc in child.Children)
                    {
                        if (sc.LocalName == "config-item-set" && sc.GetAttribute("name", OdfNamespaces.Config) == name)
                            return sc;
                    }
                    var node = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
                    node.SetAttribute("name", OdfNamespaces.Config, name, "config");
                    child.AppendChild(node);
                    return node;
                }
            }
            var sets = new OdfNode(OdfNodeType.Element, "settings", OdfNamespaces.Office, "office");
            var setNode = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
            setNode.SetAttribute("name", OdfNamespaces.Config, name, "config");
            sets.AppendChild(setNode);
            root.AppendChild(sets);
            return setNode;
        }

        protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
        {
            foreach (var child in setNode.Children)
            {
                if (child.LocalName == "config-item-map-indexed" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, "config-item-map-indexed", OdfNamespaces.Config, "config");
            node.SetAttribute("name", OdfNamespaces.Config, name, "config");
            setNode.AppendChild(node);
            return node;
        }

        protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
        {
            if (mapNode.Children.Count > 0)
                return mapNode.Children[0];
            var node = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
            mapNode.AppendChild(node);
            return node;
        }

        protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
        {
            foreach (var child in entryNode.Children)
            {
                if (child.LocalName == "config-item" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
            node.SetAttribute("name", OdfNamespaces.Config, name, "config");
            node.SetAttribute("type", OdfNamespaces.Config, type, "config");
            entryNode.AppendChild(node);
            return node;
        }

        #endregion

        #region Statistics & Document Structure Diagnostics

        protected virtual void UpdateDocumentStatistics()
        {
            int wordCount = 0;
            int charCount = 0;
            int paragraphCount = 0;
            int tableCount = 0;
            int imageCount = 0;

            TraverseForStats(ContentDom, ref wordCount, ref charCount, ref paragraphCount, ref tableCount, ref imageCount);

            var metaRoot = FindOrCreateMetaRoot();
            OdfNode? statNode = null;
            foreach (var child in metaRoot.Children)
            {
                if (child.LocalName == "document-statistic" && child.NamespaceUri == OdfNamespaces.Meta)
                {
                    statNode = child;
                    break;
                }
            }

            if (statNode == null)
            {
                statNode = new OdfNode(OdfNodeType.Element, "document-statistic", OdfNamespaces.Meta, "meta");
                metaRoot.AppendChild(statNode);
            }

            statNode.SetAttribute("word-count", OdfNamespaces.Meta, wordCount.ToString(), "meta");
            statNode.SetAttribute("character-count", OdfNamespaces.Meta, charCount.ToString(), "meta");
            statNode.SetAttribute("paragraph-count", OdfNamespaces.Meta, paragraphCount.ToString(), "meta");
            statNode.SetAttribute("table-count", OdfNamespaces.Meta, tableCount.ToString(), "meta");
            statNode.SetAttribute("image-count", OdfNamespaces.Meta, imageCount.ToString(), "meta");
            statNode.SetAttribute("page-count", OdfNamespaces.Meta, "1", "meta"); // Layout engine placeholder
        }

        private void TraverseForStats(OdfNode node, ref int words, ref int chars, ref int paragraphs, ref int tables, ref int images)
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                string text = node.TextContent;
                chars += text.Length;
                
                string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                words += parts.Length;
                return;
            }

            if (node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text) paragraphs++;
            else if (node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table) tables++;
            else if (node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw) images++;

            foreach (var child in node.Children)
            {
                TraverseForStats(child, ref words, ref chars, ref paragraphs, ref tables, ref images);
            }
        }

        #endregion

        #region Internal Merging Helpers

        private void MergeStyleNodes(OdfNode sourceContainer, OdfNode destContainer, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            foreach (var srcStyle in sourceContainer.Children)
            {
                if (srcStyle.NodeType == OdfNodeType.Element && !string.IsNullOrEmpty(srcStyle.GetAttribute("name", OdfNamespaces.Style)))
                {
                    string name = srcStyle.GetAttribute("name", OdfNamespaces.Style)!;
                    string family = srcStyle.GetAttribute("family", OdfNamespaces.Style) ?? "paragraph";

                    bool conflict = StyleEngine.StyleExists(name);

                    if (conflict && options.StyleConflictResolution == ConflictResolution.KeepSourceFormatting)
                    {
                        string newName = GenerateUniqueStyleName(name, family);
                        renameMap[name] = newName;

                        var clonedStyle = srcStyle.CloneNode(true);
                        clonedStyle.SetAttribute("name", OdfNamespaces.Style, newName, "style");
                        destContainer.AppendChild(clonedStyle);
                    }
                    else if (!conflict)
                    {
                        var clonedStyle = srcStyle.CloneNode(true);
                        destContainer.AppendChild(clonedStyle);
                    }
                }
            }
        }

        private void MergeStyles(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var sourceContentAuto = FindOrCreateChild(sourceDoc.ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
            var destContentAuto = FindOrCreateChild(ContentDom, "automatic-styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceContentAuto, destContentAuto, options, renameMap);

            var sourceStylesStyles = FindOrCreateChild(sourceDoc.StylesDom, "styles", OdfNamespaces.Office, "office");
            var destStylesStyles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceStylesStyles, destStylesStyles, options, renameMap);

            var sourceStylesAuto = FindOrCreateChild(sourceDoc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
            var destStylesAuto = FindOrCreateChild(StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
            MergeStyleNodes(sourceStylesAuto, destStylesAuto, options, renameMap);
        }

        private string GenerateUniqueStyleName(string baseName, string family = "paragraph")
        {
            int i = 1;
            string testName;
            do
            {
                testName = $"{baseName}_s{i++}";
            } while (StyleEngine.StyleExists(testName));
            return testName;
        }

        protected OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
            parent.AppendChild(node);
            return node;
        }

        protected abstract void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap);

        protected void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
        {
            var styleNameAttr = new OdfAttributeName("style-name", OdfNamespaces.Text);
            if (node.Attributes.TryGetValue(styleNameAttr, out string? currentStyleName))
            {
                if (currentStyleName != null && renameMap.TryGetValue(currentStyleName, out string? newName))
                {
                    node.Attributes[styleNameAttr] = newName;
                }
            }
            
            var drawStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Draw);
            if (node.Attributes.TryGetValue(drawStyleAttr, out string? dsName))
            {
                if (dsName != null && renameMap.TryGetValue(dsName, out string? newName))
                {
                    node.Attributes[drawStyleAttr] = newName;
                }
            }

            var tableStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Table);
            if (node.Attributes.TryGetValue(tableStyleAttr, out string? tsName))
            {
                if (tsName != null && renameMap.TryGetValue(tsName, out string? newName))
                {
                    node.Attributes[tableStyleAttr] = newName;
                }
            }

            foreach (var child in node.Children)
            {
                RemapStylesInNodes(child, renameMap);
            }
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Package.Dispose();
                }
                _isDisposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                await Package.DisposeAsync().ConfigureAwait(false);
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
