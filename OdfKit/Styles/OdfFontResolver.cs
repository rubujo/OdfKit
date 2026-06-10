using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles
{
    public static class OdfFontResolver
    {
        private static readonly Dictionary<string, string> _fontMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _customDirectories = new();
        private static bool _isScanned = false;
        private static readonly object _lock = new();

        /// <summary>
        /// Explicitly registers a font mapping.
        /// </summary>
        public static void RegisterFont(string fontName, string filePath)
        {
            if (string.IsNullOrEmpty(fontName)) throw new ArgumentNullException(nameof(fontName));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Font file not found.", filePath);

            lock (_lock)
            {
                _fontMap[fontName] = filePath;
            }
        }

        /// <summary>
        /// Registers a directory to search for font files.
        /// </summary>
        public static void RegisterFontDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) throw new ArgumentNullException(nameof(directoryPath));
            if (!Directory.Exists(directoryPath)) throw new DirectoryNotFoundException($"Font directory not found: '{directoryPath}'");

            lock (_lock)
            {
                _customDirectories.Add(directoryPath);
                _isScanned = false; // Trigger re-scan on next lookup
            }
        }

        /// <summary>
        /// Resolves the absolute path of a font by family name.
        /// </summary>
        public static string? ResolveFontPath(string fontName)
        {
            if (string.IsNullOrEmpty(fontName)) return null;

            lock (_lock)
            {
                if (_fontMap.TryGetValue(fontName, out string? path))
                {
                    return path;
                }

                if (!_isScanned)
                {
                    ScanSystemFonts();
                }

                return _fontMap.TryGetValue(fontName, out path) ? path : null;
            }
        }

        /// <summary>
        /// Scans and embeds all fonts defined in the document into the package.
        /// </summary>
        public static void EmbedFonts(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
        {
            var fontFaces = new List<OdfNode>();
            GatherFontFaces(contentRoot, fontFaces);
            GatherFontFaces(stylesRoot, fontFaces);

            foreach (var fontFace in fontFaces)
            {
                string? fontName = fontFace.GetAttribute("name", OdfNamespaces.Style);
                if (string.IsNullOrEmpty(fontName)) continue;

                string? fontPath = ResolveFontPath(fontName!);
                if (fontPath == null)
                {
                    OdfKitDiagnostics.Warn($"Could not resolve file path for font '{fontName}' to embed.");
                    continue;
                }

                // Check file size to avoid silent package bloating (warn if > 10MB)
                try
                {
                    var fileInfo = new FileInfo(fontPath);
                    if (fileInfo.Length > 10 * 1024 * 1024)
                    {
                        OdfKitDiagnostics.Warn($"Font '{fontName}' file size is large ({fileInfo.Length / 1024.0 / 1024.0:F2} MB). Embedding might cause large output files.");
                    }

                    byte[] bytes = File.ReadAllBytes(fontPath);
                    string ext = Path.GetExtension(fontPath).ToLowerInvariant();
                    string zipPath = $"Fonts/{fontName}{ext}";

                    string mediaType = ext switch
                    {
                        ".otf" => "application/x-font-opentype",
                        ".ttc" => "application/x-font-truetype-collection",
                        _ => "application/x-font-truetype"
                    };

                    // Write to package and register in manifest
                    package.WriteEntry(zipPath, bytes, mediaType);

                    // Update DOM element <style:font-face>
                    OdfNode? uriNode = null;
                    foreach (var child in fontFace.Children)
                    {
                        if (child.LocalName == "font-face-uri" && child.NamespaceUri == OdfNamespaces.Style)
                        {
                            uriNode = child;
                            break;
                        }
                    }

                    if (uriNode == null)
                    {
                        uriNode = new OdfNode(OdfNodeType.Element, "font-face-uri", OdfNamespaces.Style, "style");
                        fontFace.AppendChild(uriNode);
                    }

                    uriNode.SetAttribute("href", OdfNamespaces.XLink, zipPath, "xlink");
                    uriNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to embed font '{fontName}' from '{fontPath}': {ex.Message}");
                }
            }
        }

        private static void GatherFontFaces(OdfNode node, List<OdfNode> fontFaces)
        {
            if (node.NodeType == OdfNodeType.Element && node.LocalName == "font-face" && node.NamespaceUri == OdfNamespaces.Style)
            {
                fontFaces.Add(node);
            }
            foreach (var child in node.Children)
            {
                GatherFontFaces(child, fontFaces);
            }
        }

        private static void ScanSystemFonts()
        {
            var scanDirs = new List<string>(_customDirectories);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scanDirs.Add(@"C:\Windows\Fonts");
                string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
                if (Directory.Exists(userFonts)) scanDirs.Add(userFonts);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                scanDirs.Add("/usr/share/fonts");
                scanDirs.Add("/usr/local/share/fonts");
                string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts");
                if (Directory.Exists(userFonts)) scanDirs.Add(userFonts);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                scanDirs.Add("/Library/Fonts");
                scanDirs.Add("/System/Library/Fonts");
                string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts");
                if (Directory.Exists(userFonts)) scanDirs.Add(userFonts);
            }

            foreach (var dir in scanDirs)
            {
                ScanDirectory(dir);
            }

            _isScanned = true;
        }

        private static void ScanDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            try
            {
                var files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".ttf" || ext == ".otf" || ext == ".ttc")
                    {
                        var names = TtfReader.GetFontNames(file);
                        foreach (var name in names)
                        {
                            if (!_fontMap.ContainsKey(name))
                            {
                                _fontMap[name] = file;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to scan font directory '{dirPath}': {ex.Message}");
            }
        }
    }

    #region Pure C# TrueType/OpenType Name Table Reader

    internal static class TtfReader
    {
        public static List<string> GetFontNames(string filePath)
        {
            var names = new List<string>();
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                uint sfntVersion = ReadUInt32BE(reader);
                if (sfntVersion != 0x00010000 && sfntVersion != 0x74727565 && sfntVersion != 0x4F54544F) // 1.0, true, OTTO
                {
                    // Check if TrueType Collection (TTC)
                    if (sfntVersion == 0x74746366) // 'ttcf'
                    {
                        fs.Position = 8; // Skip version
                        uint numFonts = ReadUInt32BE(reader);
                        var offsets = new List<uint>();
                        for (int f = 0; f < numFonts; f++)
                        {
                            offsets.Add(ReadUInt32BE(reader));
                        }
                        foreach (var offset in offsets)
                        {
                            fs.Position = offset;
                            names.AddRange(ReadSingleTtfNames(reader));
                        }
                    }
                    return names;
                }

                fs.Position = 0;
                names.AddRange(ReadSingleTtfNames(reader));
            }
            catch
            {
                // Suppress errors for corrupted files
            }
            return names;
        }

        private static List<string> ReadSingleTtfNames(BinaryReader reader)
        {
            var names = new List<string>();
            var fs = reader.BaseStream;
            long fontStart = fs.Position;

            fs.Position = fontStart + 4;
            ushort numTables = ReadUInt16BE(reader);
            fs.Position = fontStart + 12;

            uint nameTableOffset = 0;
            uint nameTableLength = 0;

            for (int i = 0; i < numTables; i++)
            {
                uint tag = reader.ReadUInt32();
                uint checkSum = reader.ReadUInt32();
                uint offset = ReadUInt32BE(reader);
                uint length = ReadUInt32BE(reader);

                byte[] tagBytes = BitConverter.GetBytes(tag);
                string tagStr = Encoding.ASCII.GetString(tagBytes);
                if (tagStr == "eman" || tagStr == "name")
                {
                    nameTableOffset = offset;
                    nameTableLength = length;
                    break;
                }
            }

            if (nameTableOffset == 0 || nameTableLength == 0) return names;

            fs.Position = nameTableOffset;
            ushort format = ReadUInt16BE(reader);
            ushort count = ReadUInt16BE(reader);
            ushort stringOffset = ReadUInt16BE(reader);

            long stringStorageOffset = nameTableOffset + stringOffset;

            for (int i = 0; i < count; i++)
            {
                ushort platformId = ReadUInt16BE(reader);
                ushort encodingId = ReadUInt16BE(reader);
                ushort languageId = ReadUInt16BE(reader);
                ushort nameId = ReadUInt16BE(reader);
                ushort length = ReadUInt16BE(reader);
                ushort offset = ReadUInt16BE(reader);

                // Name ID 1 = Font Family Name, Name ID 4 = Full Font Name
                if (nameId == 1 || nameId == 4)
                {
                    long returnPos = fs.Position;
                    fs.Position = stringStorageOffset + offset;
                    byte[] bytes = reader.ReadBytes(length);
                    fs.Position = returnPos;

                    string fontName;
                    if (platformId == 3 || platformId == 0) // Windows or Unicode (UTF-16BE)
                    {
                        fontName = Encoding.BigEndianUnicode.GetString(bytes);
                    }
                    else // Macintosh (MacRoman/ASCII)
                    {
                        fontName = Encoding.ASCII.GetString(bytes);
                    }

                    fontName = fontName.Trim('\0', ' ', '\t', '\r', '\n');
                    if (!string.IsNullOrEmpty(fontName) && !names.Contains(fontName))
                    {
                        names.Add(fontName);
                    }
                }
            }

            return names;
        }

        private static ushort ReadUInt16BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static uint ReadUInt32BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }

    #endregion
}
