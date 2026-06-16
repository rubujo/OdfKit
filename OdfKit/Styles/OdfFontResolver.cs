using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 提供 ODF 文件的字型解析與內嵌功能。
/// </summary>
public static class OdfFontResolver
{
    private static readonly Dictionary<string, string> _fontMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _fallbackMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> _customDirectories = [];
    private static bool _isScanned;
    private static readonly object _lock = new();

    /// <summary>
    /// 註冊字型替代對照規則（例如在無微軟字型之 Linux/Docker 上將 "MS YaHei" 映射至 "Noto Sans CJK TC"）。
    /// </summary>
    /// <param name="targetFont">要替代的目標字型名稱。</param>
    /// <param name="replacementFont">用來替代的字型名稱。</param>
    /// <exception cref="ArgumentNullException">當參數為空時拋出。</exception>
    public static void RegisterFallback(string targetFont, string replacementFont)
    {
        if (string.IsNullOrEmpty(targetFont))
            throw new ArgumentNullException(nameof(targetFont));
        if (string.IsNullOrEmpty(replacementFont))
            throw new ArgumentNullException(nameof(replacementFont));

        lock (_lock)
        {
            _fallbackMap[targetFont] = replacementFont;
        }
    }

    /// <summary>
    /// 取得指定字型的實質替代字型名稱。若無替代規則則傳回原名稱。
    /// </summary>
    /// <param name="fontName">字型名稱。</param>
    /// <returns>替代後或原字型名稱。</returns>
    public static string MapFont(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
            return fontName;

        lock (_lock)
        {
            return _fallbackMap.TryGetValue(fontName, out string? replacement) ? replacement : fontName;
        }
    }

    /// <summary>
    /// 顯式註冊字型對應。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="filePath">字型檔案的路徑</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="fontName"/> 或 <paramref name="filePath"/> 為 null 時拋出</exception>
    /// <exception cref="FileNotFoundException">當找不到指定的字型檔案時拋出</exception>
    public static void RegisterFont(string fontName, string filePath)
    {
        if (string.IsNullOrEmpty(fontName))
            throw new ArgumentNullException(nameof(fontName));
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("找不到字型檔案。", filePath);

        lock (_lock)
        {
            _fontMap[fontName] = filePath;
        }
    }

    /// <summary>
    /// 註冊用於搜尋字型檔案的目錄。
    /// </summary>
    /// <param name="directoryPath">字型目錄的路徑</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="directoryPath"/> 為 null 時拋出</exception>
    /// <exception cref="DirectoryNotFoundException">當找不到指定的字型目錄時拋出</exception>
    public static void RegisterFontDirectory(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
            throw new ArgumentNullException(nameof(directoryPath));
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"找不到字型目錄：'{directoryPath}'");

        lock (_lock)
        {
            _customDirectories.Add(directoryPath);
            _isScanned = false; // 觸發下一次查尋時的重新掃描
        }
    }

    /// <summary>
    /// 依字型家族名稱解析字型的絕對路徑。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <returns>字型檔案的絕對路徑，若無法解析則為 null</returns>
    public static string? ResolveFontPath(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
            return null;

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
    /// 掃描並將文件中定義的所有字型內嵌至套件中。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    public static void EmbedFonts(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
    {
        List<OdfNode> fontFaces = [];
        GatherFontFaces(contentRoot, fontFaces);
        GatherFontFaces(stylesRoot, fontFaces);

        foreach (var fontFace in fontFaces)
        {
            string? fontName = fontFace.GetAttribute("name", OdfNamespaces.Style);
            if (string.IsNullOrEmpty(fontName))
                continue;

            string? fontPath = ResolveFontPath(fontName!);
            if (fontPath is null)
            {
                OdfKitDiagnostics.Warn($"無法解析字型 '{fontName}' 的檔案路徑以進行內嵌。");
                continue;
            }

            // 檢查檔案大小以避免套件無意間膨脹（若大於 10 MB 則發出警告）
            try
            {
                var fileInfo = new FileInfo(fontPath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    OdfKitDiagnostics.Warn($"字型 '{fontName}' 的檔案大小較大 ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)。內嵌可能會導致輸出檔案過大。");
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

                // 寫入套件並於指令清單中註冊
                package.WriteEntry(zipPath, bytes, mediaType);

                // 更新 DOM 項目 <style:font-face>
                OdfNode? uriNode = null;
                foreach (var child in fontFace.Children)
                {
                    if (child.LocalName == "font-face-uri" && child.NamespaceUri == OdfNamespaces.Style)
                    {
                        uriNode = child;
                        break;
                    }
                }

                if (uriNode is null)
                {
                    uriNode = new OdfNode(OdfNodeType.Element, "font-face-uri", OdfNamespaces.Style, "style");
                    fontFace.AppendChild(uriNode);
                }

                uriNode.SetAttribute("href", OdfNamespaces.XLink, zipPath, "xlink");
                uriNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"從 '{fontPath}' 內嵌字型 '{fontName}' 失敗：{ex.Message}");
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
        List<string> scanDirs = [.. _customDirectories];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            scanDirs.Add(@"C:\Windows\Fonts");
            string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
            if (Directory.Exists(userFonts))
                scanDirs.Add(userFonts);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            scanDirs.Add("/usr/share/fonts");
            scanDirs.Add("/usr/local/share/fonts");
            string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts");
            if (Directory.Exists(userFonts))
                scanDirs.Add(userFonts);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            scanDirs.Add("/Library/Fonts");
            scanDirs.Add("/System/Library/Fonts");
            string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts");
            if (Directory.Exists(userFonts))
                scanDirs.Add(userFonts);
        }

        foreach (var dir in scanDirs)
        {
            ScanDirectory(dir);
        }

        _isScanned = true;
    }

    private static void ScanDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return;
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
            OdfKitDiagnostics.Warn($"掃描字型目錄 '{dirPath}' 失敗：{ex.Message}");
        }
    }
}

#region Pure C# TrueType/OpenType Name Table Reader

internal static class TtfReader
{
    public static List<string> GetFontNames(string filePath)
    {
        List<string> names = [];
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            uint sfntVersion = ReadUInt32BE(reader);
            if (sfntVersion != 0x00010000 && sfntVersion != 0x74727565 && sfntVersion != 0x4F54544F) // 1.0, true, OTTO
            {
                // 檢查是否為 TrueType Collection (TTC)
                if (sfntVersion == 0x74746366) // 'ttcf'
                {
                    fs.Position = 8; // 跳過版本
                    uint numFonts = ReadUInt32BE(reader);
                    if (numFonts > 256)
                        return names; // 防禦性上限：真實 TTC 最多不超過 256 字型
                    List<uint> offsets = [];
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
            // 壓制損毀檔案的錯誤
        }
        return names;
    }

    private static List<string> ReadSingleTtfNames(BinaryReader reader)
    {
        List<string> names = [];
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

        if (nameTableOffset == 0 || nameTableLength == 0)
            return names;

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

            // Name ID 1 = 字型家族名稱，Name ID 4 = 完整字型名稱
            if (nameId == 1 || nameId == 4)
            {
                long returnPos = fs.Position;
                fs.Position = stringStorageOffset + offset;
                byte[] bytes = reader.ReadBytes(length);
                fs.Position = returnPos;

                string fontName;
                if (platformId == 3 || platformId == 0) // Windows 或 Unicode (UTF-16BE)
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
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static uint ReadUInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }
}

#endregion
