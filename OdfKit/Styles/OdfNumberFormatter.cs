using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 負責處理 .NET 格式字串與 ODF 數字樣式之間轉換的格式化器。
/// </summary>
public partial class OdfNumberFormatter
{
    private readonly OdfNode _contentRoot;
    private readonly OdfNode _stylesRoot;

    // 金鑰：樣式之規範 XML 金鑰，值：產生的樣式名稱（例如 N1, D1）
    private readonly Dictionary<string, string> _formatCache = new(StringComparer.Ordinal);
    private int _styleCounter;

    /// <summary>
    /// 初始化 <see cref="OdfNumberFormatter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="contentRoot"/> 或 <paramref name="stylesRoot"/> 為 null 時拋出</exception>
    public OdfNumberFormatter(OdfNode contentRoot, OdfNode stylesRoot)
    {
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _stylesRoot = stylesRoot ?? throw new ArgumentNullException(nameof(stylesRoot));
        PopulateCacheFromExistingStyles();
    }

    /// <summary>
    /// 註冊 .NET 格式字串，必要時將其翻譯為 ODF 樣式，並傳回要參考的樣式名稱。
    /// </summary>
    /// <param name="dotNetFormat">.NET 格式字串</param>
    /// <param name="culture">地區設定資訊</param>
    /// <returns>註冊或建立的樣式名稱</returns>
    public string GetOrCreateNumberStyle(string dotNetFormat, CultureInfo? culture = null)
    {
        var cult = culture ?? CultureInfo.InvariantCulture;
        string normalized = ResolveStandardFormat(dotNetFormat, cult);

        // 1. 建立格式資訊
        FormatInfo info = ParsePattern(normalized);

        // 2. 建立用於金鑰序列化的臨時樣式節點
        OdfNode tempNode = CreateStyleNode(string.Empty, info);
        string canonicalKey = SerializeOdfStyleStructure(tempNode);

        // 3. 檢查快取
        if (_formatCache.TryGetValue(canonicalKey, out string? existingStyleName))
        {
            return existingStyleName;
        }

        // 4. 判斷前綴並產生唯一名稱
        string prefix = info.Type switch
        {
            FormatType.Number => "N",
            FormatType.Currency => "C",
            FormatType.Percentage => "P",
            FormatType.Date => "D",
            FormatType.Time => "T",
            _ => "N"
        };

        string generatedName;
        do
        {
            generatedName = $"{prefix}{++_styleCounter}";
        } while (StyleExistsInDOM(generatedName));

        // 設定樣式節點名稱並附加至 DOM
        tempNode.SetAttribute("name", OdfNamespaces.Style, generatedName, "style");
        var automaticStyles = GetOrCreateAutomaticStylesNode();
        automaticStyles.AppendChild(tempNode);

        _formatCache[canonicalKey] = generatedName;
        return generatedName;
    }

    /// <summary>
    /// 將樣式名稱解析為節點。若找不到，則傳回後備的 Standard 數字樣式以防止 NullReferenceException。
    /// </summary>
    /// <param name="styleName">樣式名稱</param>
    /// <returns>解析後的樣式節點</returns>
    public OdfNode GetNumberStyleNode(string styleName)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return GetOrCreateStandardFallbackNode("Standard");
        }

        OdfNode? styleNode = FindStyleInDOM(styleName);
        if (styleNode is not null)
        {
            return styleNode;
        }

        OdfKitDiagnostics.Warn($"找不到參考的數字樣式 '{styleName}'。後退至 Standard 樣式。");
        return GetOrCreateStandardFallbackNode(styleName);
    }
}

