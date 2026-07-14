using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// Provides an isolated font registration, fallback, and segmentation context.
/// 提供隔離的字型註冊、遞補與分段情境。
/// </summary>
/// <remarks>
/// Each instance owns its font registrations, fallback mappings, plane font mappings, and subsetter,
/// so independent contexts (for example per tenant) never observe each other's state. The static APIs on
/// <see cref="OdfFontResolver"/> and <see cref="OdfFontSegmenter"/> forward to <see cref="Default"/>.
/// All members are thread-safe; lookup hot paths read immutable snapshots without locks.
/// 每個執行個體擁有自己的字型註冊、替代對照、平面字型對應與子集化器，彼此獨立的情境（例如各租戶）
/// 不會觀察到對方的狀態。<see cref="OdfFontResolver"/> 與 <see cref="OdfFontSegmenter"/> 的靜態 API
/// 一律轉發至 <see cref="Default"/>。所有成員皆為執行緒安全；查詢熱路徑以不可變快照無鎖讀取。
/// </remarks>
public sealed class OdfFontContext
{
    // 「已警告過的缺失字型名稱」快取上限：避免長駐轉換服務處理大量不重複字型名稱時，
    // 此僅供診斷用途的快取無上限成長而緩慢洩漏記憶體；超過上限時清空重來（可接受偶爾重複警告）。
    private const int MaxWarnedMissingFontsCacheSize = 2_000;

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

    private readonly object _lock = new();
    private readonly Dictionary<string, string> _fontMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fallbackMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _customDirectories = [];
    private readonly HashSet<string> _warnedMissingFonts = new(StringComparer.OrdinalIgnoreCase);
    private bool _isScanned;
    private IFontSubsetter? _fontSubsetter;
    private volatile PlaneFontMappingRegistration[] _customPlaneMappings = [];

    /// <summary>
    /// Gets the process-wide default font context.
    /// 取得行程層級的預設字型情境。
    /// </summary>
    public static OdfFontContext Default { get; } = new();

    /// <summary>
    /// Initializes a new isolated font context.
    /// 初始化新的隔離字型情境。
    /// </summary>
    public OdfFontContext()
    {
    }

    /// <summary>
    /// Registers a font file for the specified font name.
    /// 為指定字型名稱註冊字型檔案。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <param name="filePath">The font file path. / 字型檔案的路徑。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="fontName"/> 或 <paramref name="filePath"/> 為 null 時拋出</exception>
    /// <exception cref="FileNotFoundException">當找不到指定的字型檔案時拋出</exception>
    public void RegisterFont(string fontName, string filePath)
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
    /// Registers a directory scanned for font files.
    /// 註冊用於搜尋字型檔案的目錄。
    /// </summary>
    /// <param name="directoryPath">The font directory path. / 字型目錄的路徑。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="directoryPath"/> 為 null 時拋出</exception>
    /// <exception cref="DirectoryNotFoundException">當找不到指定的字型目錄時拋出</exception>
    public void RegisterFontDirectory(string directoryPath)
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
    /// Registers a font substitution rule.
    /// 註冊字型替代對照規則（例如在無微軟字型之 Linux/Docker 上將 "MS YaHei" 對照至 "Noto Sans CJK TC"）。
    /// </summary>
    /// <param name="targetFont">The font name to substitute. / 要替代的目標字型名稱。</param>
    /// <param name="replacementFont">The replacement font name. / 用來替代的字型名稱。</param>
    /// <exception cref="ArgumentNullException">當參數為空時拋出</exception>
    public void RegisterFallback(string targetFont, string replacementFont)
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
    /// Maps a font name through the registered substitution rules.
    /// 取得指定字型的實質替代字型名稱。若無替代規則則傳回原名稱。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <returns>The substituted or original font name. / 替代後或原字型名稱。</returns>
    public string MapFont(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
            return fontName;

        lock (_lock)
        {
            return _fallbackMap.TryGetValue(fontName, out string? replacement) ? replacement : fontName;
        }
    }

    /// <summary>
    /// Gets the ordered fallback candidates for a font name.
    /// 取得指定字型的解析候選序列，依序包含原始名稱、使用者註冊替代字型與內建跨平台替代字型。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <returns>The de-duplicated candidates in priority order. / 依優先順序排列且已去除重複項目的字型候選序列。</returns>
    public IReadOnlyList<string> GetFontFallbackCandidates(string fontName)
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
        }

        if (_builtInFallbackMap.TryGetValue(fontName, out string[]? builtInCandidates))
        {
            foreach (string candidate in builtInCandidates)
            {
                AddCandidate(candidate);
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
    /// Resolves the first usable fallback candidate using the specified probe.
    /// 依指定可用性探針解析第一個可使用的字型候選名稱。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <param name="isAvailable">The availability probe. / 用來判斷字型候選是否可使用的探針。</param>
    /// <returns>The first usable candidate, or null. / 第一個可使用的候選字型名稱，若沒有候選符合則為 null。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="isAvailable"/> 為 <see langword="null"/> 時擲出</exception>
    public string? ResolveFontFallback(string fontName, Func<string, bool> isAvailable)
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
    /// Resolves the absolute file path for a font family name.
    /// 依字型家族名稱解析字型的絕對路徑。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <returns>The absolute font file path, or null. / 字型檔案的絕對路徑，若無法解析則為 null。</returns>
    public string? ResolveFontPath(string fontName)
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
    /// Warns once when a font name cannot be resolved to a file.
    /// 檢查指定字型名稱是否能成功解析出實際字型檔案；若找不到則發出一次性警告（同一名稱不重複記錄）。
    /// </summary>
    /// <param name="fontName">The font name. / 字型名稱。</param>
    /// <param name="context">The warning context description. / 用於警告訊息的情境描述（例如觸發此字型查詢的功能名稱）。</param>
    /// <returns>Whether the font resolves successfully. / 若該字型可成功解析則為 <see langword="true"/>。</returns>
    public bool WarnIfUnresolvable(string fontName, string context)
    {
        if (string.IsNullOrEmpty(fontName))
            return false;

        if (ResolveFontPath(fontName) is not null)
            return true;

        lock (_lock)
        {
            if (_warnedMissingFonts.Count >= MaxWarnedMissingFontsCacheSize)
            {
                _warnedMissingFonts.Clear();
            }

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
    /// Registers a font subsetter extension.
    /// 註冊字型子集化擴充實作。
    /// </summary>
    /// <param name="subsetter">The font subsetter. / 字型子集化實作。</param>
    /// <returns>A handle restoring the previous registration when disposed. / 可用於還原先前註冊狀態的資源控制代碼。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="subsetter"/> 為 <see langword="null"/> 時擲出</exception>
    public IDisposable RegisterFontSubsetter(IFontSubsetter subsetter)
    {
        if (subsetter is null)
        {
            throw new ArgumentNullException(nameof(subsetter));
        }

        lock (_lock)
        {
            IFontSubsetter? previous = _fontSubsetter;
            _fontSubsetter = subsetter;
            return new FontSubsetterRegistration(this, previous);
        }
    }

    /// <summary>
    /// Registers a custom supplementary-plane font mapping that takes precedence over the built-in rules.
    /// 註冊自訂的增補平面字型對應規則，查詢時優先於內建對應規則。
    /// </summary>
    /// <remarks>
    /// Later registrations are consulted first. When <paramref name="baseFontPattern"/> matches the base font family
    /// (ordinal, case-insensitive substring comparison), the mapping exclusively decides the result: planes missing from
    /// <paramref name="planeFontNames"/> keep the base font family and the built-in rules are not consulted.
    /// 後註冊的規則優先比對。當 <paramref name="baseFontPattern"/> 與基礎字型家族名稱相符（不分大小寫的序數子字串比對）時，
    /// 該規則獨占決定結果：未列於 <paramref name="planeFontNames"/> 的平面維持基礎字型家族，且不再套用內建規則。
    /// </remarks>
    /// <param name="baseFontPattern">The substring matched against the base font family name. / 用於比對基礎字型家族名稱的子字串。</param>
    /// <param name="planeFontNames">The mapping from Unicode plane number (1 to 16) to the font name to use. / Unicode 平面編號（1 至 16）對應至所用字型名稱的對照表。</param>
    /// <returns>A handle that removes the registration when disposed. / 釋放時移除此註冊的資源控制代碼。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="baseFontPattern"/> 為空或 <paramref name="planeFontNames"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentOutOfRangeException">當平面編號不在 1 至 16 範圍內時擲出</exception>
    /// <exception cref="ArgumentException">當任一平面對應的字型名稱為空白時擲出</exception>
    public IDisposable RegisterSupplementaryPlaneFontMapping(string baseFontPattern, IReadOnlyDictionary<int, string> planeFontNames)
    {
        if (string.IsNullOrEmpty(baseFontPattern))
            throw new ArgumentNullException(nameof(baseFontPattern));
        if (planeFontNames is null)
            throw new ArgumentNullException(nameof(planeFontNames));

        // 防禦性複製：呼叫端後續修改原字典不得影響已註冊規則；複製後的字典不再變動，可供多執行緒無鎖讀取。
        var planeFonts = new Dictionary<int, string>(planeFontNames.Count);
        foreach (KeyValuePair<int, string> pair in planeFontNames)
        {
            if (pair.Key is < 1 or > 16)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(planeFontNames),
                    pair.Key,
                    OdfLocalizer.GetMessage("Err_OdfFontSegmenter_PlaneOutOfRange"));
            }

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfFontSegmenter_PlaneFontNameEmpty"),
                    nameof(planeFontNames));
            }

            planeFonts[pair.Key] = pair.Value;
        }

        var registration = new PlaneFontMappingRegistration(this, baseFontPattern, planeFonts);
        lock (_lock)
        {
            PlaneFontMappingRegistration[] current = _customPlaneMappings;
            var next = new PlaneFontMappingRegistration[current.Length + 1];
            next[0] = registration; // 最新註冊排最前，讓後註冊者可覆蓋先前規則
            Array.Copy(current, 0, next, 1, current.Length);
            _customPlaneMappings = next;
        }

        return registration;
    }

    /// <summary>
    /// Segments text and assigns font names per Unicode plane.
    /// 將文字依照 Unicode 字面拆分為多個文字片段，並指派適當的字型名稱。
    /// </summary>
    /// <param name="text">The source text. / 要分段的來源文字。</param>
    /// <param name="defaultFontName">The default font name. / 預設的字型名稱。</param>
    /// <returns>The text segments with font names. / 文字片段與字型名稱的 Tuple 集合。</returns>
    public List<(string Text, string FontName)> SegmentText(string text, string defaultFontName)
    {
        var result = new List<(string Text, string FontName)>();
        if (string.IsNullOrEmpty(text))
            return result;

        int i = 0;
        int len = text.Length;
        var sb = new StringBuilder();
        string currentFont = defaultFontName;

        while (i < len)
        {
            int codePoint;
            int charCount;
            if (char.IsHighSurrogate(text[i]) && i + 1 < len && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                charCount = 2;
            }
            else
            {
                codePoint = text[i];
                charCount = 1;
            }

            int plane = codePoint >> 16;
            string targetFont = defaultFontName;

            if (plane >= 1 && GetCustomPlaneFontName(defaultFontName, plane) is string customFont)
            {
                // 自訂規則可涵蓋任何增補平面（含 Plane 1 SMP 與未來新增區塊）。
                targetFont = customFont;
            }
            else if (plane == 2 || plane == 3 || plane == 15 || plane == 16)
            {
                // 內建規則維持既有行為：僅處理 CJK 罕字實際使用的平面。
                targetFont = GetSupplementaryPlaneFontName(defaultFontName, plane);
            }

            if (targetFont != currentFont)
            {
                if (sb.Length > 0)
                {
                    result.Add((sb.ToString(), currentFont));
                    sb.Clear();
                }
                currentFont = targetFont;
            }

            if (charCount == 2)
            {
                sb.Append(text[i]);
                sb.Append(text[i + 1]);
            }
            else
            {
                sb.Append(text[i]);
            }

            i += charCount;
        }

        if (sb.Length > 0)
        {
            result.Add((sb.ToString(), currentFont));
        }

        return result;
    }

    /// <summary>
    /// Gets the font name for a base font family and Unicode plane.
    /// 依據基礎字型名稱與 Unicode 平面，取得對應的字型名稱（自訂註冊規則優先，其後為內建的全字庫、花園明朝與字雲等對應）。
    /// </summary>
    /// <param name="baseFontFamily">The base font family name. / 基礎字型名稱。</param>
    /// <param name="plane">The Unicode plane. / Unicode 平面（Plane）。</param>
    /// <returns>The mapped font name. / 對應的字型名稱。</returns>
    public string GetSupplementaryPlaneFontName(string baseFontFamily, int plane)
    {
        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = OdfFontSegmenter.DefaultBaseFontFamily;

        // 0. 自訂註冊規則優先於所有內建對應
        if (GetCustomPlaneFontName(baseFontFamily, plane) is string customFontName)
        {
            return customFontName;
        }

        return OdfFontSegmenter.GetBuiltInSupplementaryPlaneFontName(baseFontFamily, plane);
    }

    /// <summary>
    /// Embeds all fonts declared in the document into the package.
    /// 掃描並將文件中定義的所有字型內嵌至套件中。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 套件。</param>
    /// <param name="contentRoot">The content XML root. / 內容 XML 的根節點。</param>
    /// <param name="stylesRoot">The styles XML root. / 樣式 XML 的根節點。</param>
    public void EmbedFonts(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
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
    /// Embeds subset fonts for private-use code points when a subsetter is registered.
    /// 若已註冊字型子集化實作，掃描文件中的 PUA 自造字並將對應子集字型嵌入封裝。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 套件。</param>
    /// <param name="contentRoot">The content XML root. / 內容 XML 的根節點。</param>
    /// <param name="stylesRoot">The styles XML root. / 樣式 XML 的根節點。</param>
    public void EmbedFontSubsets(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
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

    /// <summary>
    /// 查詢自訂平面對應規則；命中規則時傳回對應字型名稱（該平面未設定時傳回基礎字型家族），無任何規則命中時傳回 null。
    /// </summary>
    private string? GetCustomPlaneFontName(string baseFontFamily, int plane)
    {
        // 單次 volatile 讀取取得不可變快照；無註冊時以長度檢查快速返回，維持熱路徑零額外成本。
        PlaneFontMappingRegistration[] customMappings = _customPlaneMappings;
        if (customMappings.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(baseFontFamily))
            baseFontFamily = OdfFontSegmenter.DefaultBaseFontFamily;

        foreach (PlaneFontMappingRegistration mapping in customMappings)
        {
            if (baseFontFamily.Contains(mapping.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.PlaneFonts.TryGetValue(plane, out string? fontName) ? fontName : baseFontFamily;
            }
        }

        return null;
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

    private void ScanSystemFonts()
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

    private void ScanDirectory(string dirPath)
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

    private sealed class FontSubsetterRegistration(OdfFontContext owner, IFontSubsetter? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (owner._lock)
            {
                owner._fontSubsetter = previous;
            }

            _disposed = true;
        }
    }

    private sealed class PlaneFontMappingRegistration(OdfFontContext owner, string pattern, Dictionary<int, string> planeFonts) : IDisposable
    {
        internal string Pattern { get; } = pattern;

        internal Dictionary<int, string> PlaneFonts { get; } = planeFonts;

        public void Dispose()
        {
            lock (owner._lock)
            {
                PlaneFontMappingRegistration[] current = owner._customPlaneMappings;
                int index = Array.IndexOf(current, this);
                if (index < 0)
                {
                    // 已被移除（重複 Dispose）：直接返回即可，維持冪等性。
                    return;
                }

                var next = new PlaneFontMappingRegistration[current.Length - 1];
                Array.Copy(current, 0, next, 0, index);
                Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                owner._customPlaneMappings = next;
            }
        }
    }
}
