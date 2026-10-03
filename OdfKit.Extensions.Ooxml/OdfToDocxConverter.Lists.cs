using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// ODT to DOCX list conversion: ODF list styles become numbering definitions, list items become numbered paragraphs.
/// ODT → DOCX 清單轉換：ODF 清單樣式轉成編號定義，清單項目轉成帶編號的段落。
/// </summary>
public static partial class OdfToDocxConverter
{
    private const int MaxListLevels = 9;

    /// <summary>
    /// 收集轉換期間用到的編號定義：每個 ODF 清單樣式一個 <c>w:abstractNum</c>，每個頂層清單一個 <c>w:num</c>
    /// （ODF 的清單預設各自重新編號，除非標示延續編號）。
    /// </summary>
    private sealed class NumberingBuilder
    {
        private readonly Dictionary<string, int> _abstractIds = new(StringComparer.Ordinal);
        private readonly List<WP.AbstractNum> _abstracts = [];
        private readonly List<WP.NumberingInstance> _instances = [];
        private readonly Dictionary<string, int> _lastInstanceByStyle = new(StringComparer.Ordinal);

        internal bool HasContent => _instances.Count > 0;

        /// <summary>
        /// 取得頂層清單使用的編號編號（<c>w:numId</c>）；延續編號時沿用同一樣式上一個清單的編號。
        /// </summary>
        internal int GetNumberingId(string styleKey, OdfNode? listStyle, bool continueNumbering)
        {
            if (continueNumbering && _lastInstanceByStyle.TryGetValue(styleKey, out int existing))
            {
                return existing;
            }

            if (!_abstractIds.TryGetValue(styleKey, out int abstractId))
            {
                abstractId = _abstracts.Count;
                _abstractIds[styleKey] = abstractId;
                _abstracts.Add(CreateAbstractNum(abstractId, listStyle));
            }

            int numberingId = _instances.Count + 1;
            var instance = new WP.NumberingInstance(new WP.AbstractNumId { Val = abstractId }) { NumberID = numberingId };
            WP.AbstractNum abstractNum = _abstracts[abstractId];
            foreach (WP.Level level in abstractNum.Elements<WP.Level>())
            {
                // 每個頂層清單從該層的起始值重新編號。
                int start = level.StartNumberingValue?.Val?.Value ?? 1;
                instance.AppendChild(new WP.LevelOverride(new WP.StartOverrideNumberingValue { Val = start })
                {
                    LevelIndex = level.LevelIndex,
                });
            }

            _instances.Add(instance);
            _lastInstanceByStyle[styleKey] = numberingId;
            return numberingId;
        }

        internal WP.Numbering Build()
        {
            var numbering = new WP.Numbering();
            foreach (WP.AbstractNum abstractNum in _abstracts)
            {
                numbering.AppendChild(abstractNum);
            }

            foreach (WP.NumberingInstance instance in _instances)
            {
                numbering.AppendChild(instance);
            }

            return numbering;
        }
    }

    private static WP.AbstractNum CreateAbstractNum(int abstractId, OdfNode? listStyle)
    {
        var abstractNum = new WP.AbstractNum { AbstractNumberId = abstractId };
        abstractNum.AppendChild(new WP.MultiLevelType { Val = WP.MultiLevelValues.HybridMultilevel });

        for (int level = 0; level < MaxListLevels; level++)
        {
            OdfNode? levelStyle = listStyle?.Children.FirstOrDefault(child =>
                child.NamespaceUri == OdfNamespaces.Text
                && child.LocalName.StartsWith("list-level-style-", StringComparison.Ordinal)
                && (child.GetAttribute("level", OdfNamespaces.Text) ?? string.Empty)
                    == (level + 1).ToString(CultureInfo.InvariantCulture));
            abstractNum.AppendChild(CreateListLevel(level, levelStyle));
        }

        return abstractNum;
    }

    private static WP.Level CreateListLevel(int level, OdfNode? levelStyle)
    {
        string format = "bullet";
        string text = "•";
        int start = 1;

        if (levelStyle is not null && levelStyle.LocalName == "list-level-style-number")
        {
            format = MapNumberFormat(levelStyle.GetAttribute("num-format", OdfNamespaces.Style));
            start = int.TryParse(levelStyle.GetAttribute("start-value", OdfNamespaces.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedStart) && parsedStart > 0
                ? parsedStart
                : 1;
            text = BuildNumberLevelText(levelStyle, level);
        }
        else if (levelStyle is not null && levelStyle.LocalName == "list-level-style-bullet")
        {
            text = NormalizeBulletCharacter(levelStyle.GetAttribute("bullet-char", OdfNamespaces.Text));
        }
        else if (levelStyle is not null)
        {
            // 圖片項目符號等沒有對應的編號格式：以一般項目符號代替。
        }

        (int left, int hanging) = ReadListLevelIndentation(levelStyle, level);
        var result = new WP.Level(
            new WP.StartNumberingValue { Val = start },
            new WP.NumberingFormat { Val = new WP.NumberFormatValues(format) },
            new WP.LevelText { Val = text },
            new WP.LevelJustification { Val = WP.LevelJustificationValues.Left },
            new WP.PreviousParagraphProperties(
                new WP.Indentation
                {
                    Left = left.ToString(CultureInfo.InvariantCulture),
                    Hanging = hanging.ToString(CultureInfo.InvariantCulture),
                }))
        {
            LevelIndex = level,
        };
        return result;
    }

    // ODF 的 num-format 字串對應 OOXML 的 numFmt；未知格式退回十進位。
    private static string MapNumberFormat(string? odfFormat)
    {
        switch (odfFormat)
        {
            case "1":
                return "decimal";
            case "a":
                return "lowerLetter";
            case "A":
                return "upperLetter";
            case "i":
                return "lowerRoman";
            case "I":
                return "upperRoman";
            case "":
                return "none";
            case "一, 二, 三, ...":
                return "taiwaneseCounting";
            case "壹, 貳, 參, ...":
                return "ideographLegalTraditional";
            default:
                return "decimal";
        }
    }

    // 前綴 + 各層編號佔位 + 後綴；顯示多層編號（display-levels）時加入上層的佔位（例如 1.2.）。
    private static string BuildNumberLevelText(OdfNode levelStyle, int level)
    {
        string prefix = levelStyle.GetAttribute("num-prefix", OdfNamespaces.Style) ?? string.Empty;
        string suffix = levelStyle.GetAttribute("num-suffix", OdfNamespaces.Style) ?? string.Empty;
        int displayLevels = int.TryParse(levelStyle.GetAttribute("display-levels", OdfNamespaces.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Max(1, Math.Min(parsed, level + 1))
            : 1;

        var builder = new System.Text.StringBuilder(prefix);
        for (int index = level - displayLevels + 1; index <= level; index++)
        {
            builder.Append('%').Append((index + 1).ToString(CultureInfo.InvariantCulture));
            if (index < level)
            {
                builder.Append('.');
            }
        }

        builder.Append(suffix);
        return builder.ToString();
    }

    // LibreOffice 的項目符號常是 OpenSymbol 私用區字元，Word 沒有這個字型時會顯示成方塊；改用標準圓點。
    private static string NormalizeBulletCharacter(string? bullet)
    {
        if (string.IsNullOrEmpty(bullet))
        {
            return "•";
        }

        char first = bullet![0];
        return first >= '' && first <= '' ? "•" : bullet.Substring(0, 1);
    }

    // 縮排：label-alignment 模式取 margin-left 與 text-indent；舊模式為 space-before 加上標籤寬度。
    private static (int Left, int Hanging) ReadListLevelIndentation(OdfNode? levelStyle, int level)
    {
        int defaultLeft = 720 * (level + 1);
        const int defaultHanging = 360;
        OdfNode? properties = levelStyle?.Children.FirstOrDefault(
            child => child.NamespaceUri == OdfNamespaces.Style && child.LocalName == "list-level-properties");
        if (properties is null)
        {
            return (defaultLeft, defaultHanging);
        }

        OdfNode? alignment = properties.Children.FirstOrDefault(
            child => child.NamespaceUri == OdfNamespaces.Style && child.LocalName == "list-level-label-alignment");
        if (alignment is not null)
        {
            int? marginLeft = ParseLengthToTwips(alignment.GetAttribute("margin-left", OdfNamespaces.Fo));
            int? textIndent = ParseLengthToTwips(alignment.GetAttribute("text-indent", OdfNamespaces.Fo));
            if (marginLeft is int left)
            {
                return (Math.Max(0, left), Math.Max(0, -(textIndent ?? -defaultHanging)));
            }
        }

        int? spaceBefore = ParseLengthToTwips(properties.GetAttribute("space-before", OdfNamespaces.Text));
        int? labelWidth = ParseLengthToTwips(properties.GetAttribute("min-label-width", OdfNamespaces.Text));
        if (spaceBefore is not null || labelWidth is not null)
        {
            int width = labelWidth ?? defaultHanging;
            return (Math.Max(0, (spaceBefore ?? 0) + width), Math.Max(0, width));
        }

        return (defaultLeft, defaultHanging);
    }

    private static int? ParseLengthToTwips(string? length)
    {
        if (string.IsNullOrWhiteSpace(length))
        {
            return null;
        }

        string text = length!.Trim();
        double factor;
        string number;
        if (text.EndsWith("cm", StringComparison.Ordinal))
        {
            factor = 1440d / 2.54d;
            number = text.Substring(0, text.Length - 2);
        }
        else if (text.EndsWith("mm", StringComparison.Ordinal))
        {
            factor = 1440d / 25.4d;
            number = text.Substring(0, text.Length - 2);
        }
        else if (text.EndsWith("in", StringComparison.Ordinal))
        {
            factor = 1440d;
            number = text.Substring(0, text.Length - 2);
        }
        else if (text.EndsWith("pt", StringComparison.Ordinal))
        {
            factor = 20d;
            number = text.Substring(0, text.Length - 2);
        }
        else
        {
            return null;
        }

        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? (int)Math.Round(value * factor)
            : null;
    }

    /// <summary>
    /// 轉換 <c>text:list</c>：項目內的段落帶上編號屬性，巢狀清單的層級加一，並維持原本的文件順序。
    /// </summary>
    private static void ConvertList(
        OdfNode list,
        OpenXmlElement target,
        MainDocumentPart mainPart,
        ConversionContext ctx,
        OdfPackage odtPackage,
        Dictionary<string, ImagePart> imagePartCache,
        TrackedChangeRegistry trackedChanges,
        int level,
        int? inheritedNumberingId,
        string? inheritedStyleName)
    {
        string? styleName = list.GetAttribute("style-name", OdfNamespaces.Text) ?? inheritedStyleName;
        int numberingId;
        if (inheritedNumberingId is int inherited)
        {
            numberingId = inherited;
        }
        else
        {
            ctx.ListStyles.TryGetValue(styleName ?? string.Empty, out OdfNode? listStyle);
            bool continueNumbering = string.Equals(
                list.GetAttribute("continue-numbering", OdfNamespaces.Text), "true", StringComparison.Ordinal);
            numberingId = ctx.Numbering.GetNumberingId(styleName ?? string.Empty, listStyle, continueNumbering);
        }

        foreach (OdfNode item in list.Children)
        {
            if (item.NamespaceUri != OdfNamespaces.Text)
            {
                continue;
            }

            bool isHeader = item.LocalName == "list-header";
            if (item.LocalName != "list-item" && !isHeader)
            {
                continue;
            }

            foreach (OdfNode content in item.Children)
            {
                if (content.NamespaceUri == OdfNamespaces.Text && content.LocalName == "list")
                {
                    ConvertList(content, target, mainPart, ctx, odtPackage, imagePartCache, trackedChanges, level + 1, numberingId, styleName);
                }
                else if (content.NamespaceUri == OdfNamespaces.Text && (content.LocalName == "p" || content.LocalName == "h"))
                {
                    WP.Paragraph paragraph = content.LocalName == "p"
                        ? ConvertParagraph(content, mainPart, ctx, odtPackage, imagePartCache, trackedChanges)
                        : ConvertHeading(content, mainPart, ctx, odtPackage, imagePartCache, trackedChanges);
                    if (!isHeader)
                    {
                        WP.ParagraphProperties properties = GetOrCreateParagraphProperties(paragraph);
                        properties.AppendChild(new WP.NumberingProperties(
                            new WP.NumberingLevelReference { Val = Math.Min(level, MaxListLevels - 1) },
                            new WP.NumberingId { Val = numberingId }));
                    }

                    target.AppendChild(paragraph);
                }
                else if (content.NamespaceUri == OdfNamespaces.Table && content.LocalName == "table")
                {
                    target.AppendChild(ConvertTable(content, mainPart, ctx, odtPackage, imagePartCache, trackedChanges));
                }
            }
        }
    }
}
