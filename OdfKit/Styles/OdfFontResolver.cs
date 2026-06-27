using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Styles;

/// <summary>
/// 提供 ODF 文件的字型解析與內嵌功能。
/// </summary>
public static class OdfFontResolver
{
    private static readonly Dictionary<string, string> _fontMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _fallbackMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> _builtInFallbackMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aptos"] = ["Arial", "Liberation Sans", "DejaVu Sans"],
        ["Aptos Display"] = ["Arial", "Liberation Sans", "DejaVu Sans"],
        ["Calibri"] = ["Carlito", "Arial", "Liberation Sans", "DejaVu Sans"],
        ["Cambria"] = ["Caladea", "Times New Roman", "Liberation Serif", "DejaVu Serif"],
        ["Consolas"] = ["Cascadia Mono", "Courier New", "Liberation Mono", "DejaVu Sans Mono"],
        ["Courier New"] = ["Liberation Mono", "DejaVu Sans Mono"],
        ["Microsoft JhengHei"] = ["Noto Sans CJK TC", "Source Han Sans TC", "Noto Sans TC", "DejaVu Sans"],
        ["MingLiU"] = ["Noto Serif CJK TC", "Source Han Serif TC", "Noto Serif TC", "DejaVu Serif"],
        ["PMingLiU"] = ["Noto Serif CJK TC", "Source Han Serif TC", "Noto Serif TC", "DejaVu Serif"],
        ["Times New Roman"] = ["Liberation Serif", "DejaVu Serif"],
        ["微軟正黑體"] = ["Noto Sans CJK TC", "Source Han Sans TC", "Noto Sans TC", "DejaVu Sans"],
        ["細明體"] = ["Noto Serif CJK TC", "Source Han Serif TC", "Noto Serif TC", "DejaVu Serif"],
        ["新細明體"] = ["Noto Serif CJK TC", "Source Han Serif TC", "Noto Serif TC", "DejaVu Serif"]
    };

    private static readonly List<string> _customDirectories = [];
    private static readonly HashSet<string> _warnedMissingFonts = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isScanned;
    private static readonly object _lock = new();
    private static IFontSubsetter? _fontSubsetter;

    /// <summary>
    /// 檢查指定字型名稱是否能成功解析出實際字型檔案；若找不到則發出一次性警告（同一名稱不重複記錄），
    /// 避免使用者在缺少全字庫／花園明朝／字雲等超大字型時，毫無線索地得到顯示為空白方塊的文字。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="context">用於警告訊息的情境描述（例如觸發此字型查詢的功能名稱）</param>
    /// <returns>若該字型可成功解析則為 <see langword="true"/></returns>
    public static bool WarnIfUnresolvable(string fontName, string context)
    {
        if (string.IsNullOrEmpty(fontName))
            return false;

        if (ResolveFontPath(fontName) is not null)
            return true;

        lock (_lock)
        {
            if (!_warnedMissingFonts.Add(fontName))
                return false;
        }

        OdfKitDiagnostics.Warn(
            $"找不到字型「{fontName}」對應的檔案（{context}）。此名稱通常用於顯示 CNS 11643 高位字面或其他罕見 Unicode 補充平面字元，" +
            "若系統未安裝對應字型（例如全字庫、花園明朝、字雲），這些字元可能會顯示為空白方塊。" +
            "可呼叫 OdfFontResolver.RegisterFont 或 RegisterFontDirectory 註冊實際字型檔案位置。");
        return false;
    }

    /// <summary>
    /// 檢查指定字型檔案是否為 TrueType Collection（.ttc）格式。PDFsharp 等部分渲染後端不支援直接讀取
    /// TTC 容器，須在使用前偵測並改用替代字型，否則可能拋出例外或無法正確顯示文字。
    /// </summary>
    /// <param name="filePath">字型檔案路徑</param>
    /// <returns>若檔案以 TTC 簽章（'ttcf'）開頭則為 <see langword="true"/></returns>
    public static bool IsTrueTypeCollection(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] signature = new byte[4];
            if (fs.Read(signature, 0, 4) != 4)
                return false;

            // 'ttcf' 的大端序位元組序列。
            return signature[0] == 0x74 && signature[1] == 0x74 && signature[2] == 0x63 && signature[3] == 0x66;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 註冊字型替代對照規則（例如在無微軟字型之 Linux/Docker 上將 "MS YaHei" 對照至 "Noto Sans CJK TC"）。
    /// </summary>
    /// <param name="targetFont">要替代的目標字型名稱</param>
    /// <param name="replacementFont">用來替代的字型名稱</param>
    /// <exception cref="ArgumentNullException">當參數為空時拋出</exception>
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
    /// <param name="fontName">字型名稱</param>
    /// <returns>替代後或原字型名稱</returns>
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
    /// 取得指定字型的解析候選序列，依序包含原始名稱、使用者註冊替代字型與內建跨平台替代字型。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <returns>依優先順序排列且已去除重複項目的字型候選序列</returns>
    public static IReadOnlyList<string> GetFontFallbackCandidates(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return [];
        }

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCandidate(fontName);

        lock (_lock)
        {
            if (_fallbackMap.TryGetValue(fontName, out string? replacement))
            {
                AddCandidate(replacement);
            }

            if (_builtInFallbackMap.TryGetValue(fontName, out string[]? builtInCandidates))
            {
                foreach (string candidate in builtInCandidates)
                {
                    AddCandidate(candidate);
                }
            }
        }

        return candidates;

        void AddCandidate(string candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }
    }

    /// <summary>
    /// 依指定可用性探針解析第一個可使用的字型候選名稱。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="isAvailable">用來判斷字型候選是否可使用的探針</param>
    /// <returns>第一個可使用的候選字型名稱，若沒有候選符合則為 null</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="isAvailable"/> 為 <see langword="null"/> 時擲出</exception>
    public static string? ResolveFontFallback(string fontName, Func<string, bool> isAvailable)
    {
        if (isAvailable is null)
        {
            throw new ArgumentNullException(nameof(isAvailable));
        }

        foreach (string candidate in GetFontFallbackCandidates(fontName))
        {
            if (isAvailable(candidate))
            {
                return candidate;
            }
        }

        return null;
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
            throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_OdfFontResolver_FontNotFound"), filePath);

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
            throw new DirectoryNotFoundException(OdfLocalizer.GetMessage("Err_OdfFontResolver_FontNotFound_2", directoryPath));

        lock (_lock)
        {
            _customDirectories.Add(directoryPath);
            _isScanned = false; // 觸發下一次查尋時的重新掃描
        }
    }

    /// <summary>
    /// 註冊字型子集化擴充實作。
    /// </summary>
    /// <param name="subsetter">字型子集化實作</param>
    /// <returns>可用於還原先前註冊狀態的資源控制代碼</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="subsetter"/> 為 <see langword="null"/> 時擲出</exception>
    public static IDisposable RegisterFontSubsetter(IFontSubsetter subsetter)
    {
        if (subsetter is null)
        {
            throw new ArgumentNullException(nameof(subsetter));
        }

        lock (_lock)
        {
            IFontSubsetter? previous = _fontSubsetter;
            _fontSubsetter = subsetter;
            return new FontSubsetterRegistration(previous);
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

                // 更新 DOM 專案 <style:font-face>
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

    /// <summary>
    /// 若已註冊字型子集化實作，掃描文件中的 PUA 自造字並將對應子集字型嵌入封裝。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    public static void EmbedFontSubsets(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        if (contentRoot is null)
        {
            throw new ArgumentNullException(nameof(contentRoot));
        }

        if (stylesRoot is null)
        {
            throw new ArgumentNullException(nameof(stylesRoot));
        }

        IFontSubsetter? subsetter;
        lock (_lock)
        {
            subsetter = _fontSubsetter;
        }

        if (subsetter is null)
        {
            return;
        }

        SortedSet<int> codePoints = [];
        GatherPrivateUseCodePoints(contentRoot, codePoints);
        if (codePoints.Count == 0)
        {
            return;
        }

        List<OdfNode> fontFaces = [];
        GatherFontFaces(contentRoot, fontFaces);
        GatherFontFaces(stylesRoot, fontFaces);
        var processedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (OdfNode fontFace in fontFaces)
        {
            string? fontName = fontFace.GetAttribute("name", OdfNamespaces.Style);
            if (string.IsNullOrEmpty(fontName) || !processedFonts.Add(fontName!))
            {
                continue;
            }

            string? fontPath = ResolveFontPath(fontName!);
            var request = new OdfFontSubsetRequest(fontName!, fontPath, codePoints);
            OdfFontSubset? subset;
            try
            {
                subset = subsetter.CreateSubset(request);
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"字型 '{fontName}' 子集化失敗：{ex.Message}");
                continue;
            }

            if (subset is null || subset.Bytes.Length == 0)
            {
                continue;
            }

            string path = $"Fonts/Subsets/{SanitizePackagePathSegment(fontName!)}-subset{subset.Extension}";
            package.WriteEntry(path, subset.Bytes, subset.MediaType);
            LinkFontSubset(contentRoot, fontName!, path);
            LinkFontSubset(stylesRoot, fontName!, path);
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

    private static void GatherPrivateUseCodePoints(OdfNode node, SortedSet<int> codePoints)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            for (int i = 0; i < text.Length; i++)
            {
                int codePoint = char.ConvertToUtf32(text, i);
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                if (IsPrivateUseCodePoint(codePoint))
                {
                    codePoints.Add(codePoint);
                }
            }
        }

        foreach (OdfNode child in node.Children)
        {
            GatherPrivateUseCodePoints(child, codePoints);
        }
    }

    private static bool IsPrivateUseCodePoint(int codePoint)
        => codePoint is >= 0xE000 and <= 0xF8FF
            or >= 0xF0000 and <= 0xFFFFD
            or >= 0x100000 and <= 0x10FFFD;

    private static void LinkFontSubset(OdfNode root, string fontName, string packagePath)
    {
        List<OdfNode> fontFaces = [];
        GatherFontFaces(root, fontFaces);
        foreach (OdfNode fontFace in fontFaces)
        {
            if (fontFace.GetAttribute("name", OdfNamespaces.Style) == fontName)
            {
                OdfNode uriNode = FindOrCreateFontFaceUri(fontFace);
                uriNode.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
                uriNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            }
        }
    }

    private static OdfNode FindOrCreateFontFaceUri(OdfNode fontFace)
    {
        foreach (OdfNode child in fontFace.Children)
        {
            if (child.LocalName == "font-face-uri" && child.NamespaceUri == OdfNamespaces.Style)
            {
                return child;
            }
        }

        OdfNode uriNode = new(OdfNodeType.Element, "font-face-uri", OdfNamespaces.Style, "style");
        fontFace.AppendChild(uriNode);
        return uriNode;
    }

    private static string SanitizePackagePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            builder.Append(IsSafePackagePathCharacter(ch) ? ch : '_');
        }

        return builder.Length == 0 ? "font" : builder.ToString();
    }

    private static bool IsSafePackagePathCharacter(char ch)
        => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.';

    private sealed class FontSubsetterRegistration(IFontSubsetter? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                _fontSubsetter = previous;
            }

            _disposed = true;
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
                    var names = TtfFontNameReader.GetFontNames(file);
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
