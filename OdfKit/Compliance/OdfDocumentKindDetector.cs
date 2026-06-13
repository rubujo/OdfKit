#pragma warning restore CS1591
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 描述單一 ODF 檔案格式的副檔名、MIME 類型與文件種類。
/// </summary>
/// <param name="extension">包含前導句點的檔案副檔名。</param>
/// <param name="mimeType">ODF 封裝或扁平 XML 宣告的 MIME 類型。</param>
/// <param name="kind">對應的 ODF 文件種類。</param>
/// <param name="bodyKind">對應於 <c>office:body</c> 子元素的內容種類。</param>
/// <param name="isFlatXml">是否為單一 XML (Flat XML) 格式。</param>
public sealed class OdfFormatInfo(
    string extension,
    string mimeType,
    OdfDocumentKind kind,
    OdfDocumentKind bodyKind,
    bool isFlatXml)
{
    /// <summary>
    /// 取得包含前導句點的檔案副檔名。
    /// </summary>
    public string Extension { get; } = extension ?? throw new ArgumentNullException(nameof(extension));

    /// <summary>
    /// 取得 ODF 封裝或扁平 XML 宣告的 MIME 類型。
    /// </summary>
    public string MimeType { get; } = mimeType ?? throw new ArgumentNullException(nameof(mimeType));

    /// <summary>
    /// 取得對應的 ODF 文件種類。
    /// </summary>
    public OdfDocumentKind Kind { get; } = kind;

    /// <summary>
    /// 取得對應於 <c>office:body</c> 子元素的內容種類。
    /// </summary>
    public OdfDocumentKind BodyKind { get; } = bodyKind;

    /// <summary>
    /// 取得是否為單一 XML (Flat XML) 格式。
    /// </summary>
    public bool IsFlatXml { get; } = isFlatXml;
}

/// <summary>
/// 從 MIME 類型與副檔名偵測 ODF 文件種類。
/// </summary>
public static class OdfDocumentKindDetector
{
    private const string TextMimeType = "application/vnd.oasis.opendocument.text";
    private const string SpreadsheetMimeType = "application/vnd.oasis.opendocument.spreadsheet";
    private const string PresentationMimeType = "application/vnd.oasis.opendocument.presentation";
    private const string GraphicsMimeType = "application/vnd.oasis.opendocument.graphics";

    private static readonly OdfFormatInfo[] FormatTable =
    [
        new(".odt", TextMimeType, OdfDocumentKind.Text, OdfDocumentKind.Text, false),
        new(".ott", "application/vnd.oasis.opendocument.text-template", OdfDocumentKind.TextTemplate, OdfDocumentKind.Text, false),
        new(".odm", "application/vnd.oasis.opendocument.text-master", OdfDocumentKind.TextMaster, OdfDocumentKind.Text, false),
        new(".ods", SpreadsheetMimeType, OdfDocumentKind.Spreadsheet, OdfDocumentKind.Spreadsheet, false),
        new(".ots", "application/vnd.oasis.opendocument.spreadsheet-template", OdfDocumentKind.SpreadsheetTemplate, OdfDocumentKind.Spreadsheet, false),
        new(".odp", PresentationMimeType, OdfDocumentKind.Presentation, OdfDocumentKind.Presentation, false),
        new(".otp", "application/vnd.oasis.opendocument.presentation-template", OdfDocumentKind.PresentationTemplate, OdfDocumentKind.Presentation, false),
        new(".odg", GraphicsMimeType, OdfDocumentKind.Graphics, OdfDocumentKind.Graphics, false),
        new(".otg", "application/vnd.oasis.opendocument.graphics-template", OdfDocumentKind.GraphicsTemplate, OdfDocumentKind.Graphics, false),
        new(".odc", "application/vnd.oasis.opendocument.chart", OdfDocumentKind.Chart, OdfDocumentKind.Chart, false),
        new(".odf", "application/vnd.oasis.opendocument.formula", OdfDocumentKind.Formula, OdfDocumentKind.Formula, false),
        new(".odi", "application/vnd.oasis.opendocument.image", OdfDocumentKind.Image, OdfDocumentKind.Image, false),
        new(".odb", "application/vnd.oasis.opendocument.database", OdfDocumentKind.Database, OdfDocumentKind.Database, false),
        new(".fodt", TextMimeType, OdfDocumentKind.FlatText, OdfDocumentKind.Text, true),
        new(".fods", SpreadsheetMimeType, OdfDocumentKind.FlatSpreadsheet, OdfDocumentKind.Spreadsheet, true),
        new(".fodp", PresentationMimeType, OdfDocumentKind.FlatPresentation, OdfDocumentKind.Presentation, true),
        new(".fodg", GraphicsMimeType, OdfDocumentKind.FlatGraphics, OdfDocumentKind.Graphics, true)
    ];

    private static readonly ReadOnlyCollection<OdfFormatInfo> SupportedFormatsValue = Array.AsReadOnly(FormatTable);

    private static readonly Dictionary<string, OdfFormatInfo> MimeTypeFormats = FormatTable
        .Where(format => !format.IsFlatXml)
        .ToDictionary(format => format.MimeType, format => format, StringComparer.Ordinal);

    private static readonly Dictionary<string, OdfFormatInfo> ExtensionFormats = FormatTable
        .ToDictionary(format => format.Extension, format => format, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 取得目前支援偵測的 ODF 格式描述清單。
    /// </summary>
    public static IReadOnlyList<OdfFormatInfo> SupportedFormats => SupportedFormatsValue;

    /// <summary>
    /// 從 ODF MIME 類型偵測封裝的 ODF 文件種類。
    /// </summary>
    /// <param name="mimeType">MIME 類型</param>
    /// <returns>偵測到的 ODF 文件種類；若無法識別則傳回 <see cref="OdfDocumentKind.Unknown"/></returns>
    public static OdfDocumentKind FromMimeType(string? mimeType)
    {
        return TryGetFormatByMimeType(mimeType, out OdfFormatInfo? format)
            ? format!.Kind
            : OdfDocumentKind.Unknown;
    }

    /// <summary>
    /// 從檔案名稱或副檔名偵測 ODF 文件種類。
    /// </summary>
    /// <param name="fileName">檔案名稱或副檔名</param>
    /// <returns>偵測到的 ODF 文件種類；若無法識別則傳回 <see cref="OdfDocumentKind.Unknown"/></returns>
    public static OdfDocumentKind FromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return OdfDocumentKind.Unknown;
        }

        return TryGetFormatByFileName(fileName, out OdfFormatInfo? format)
            ? format!.Kind
            : OdfDocumentKind.Unknown;
    }

    /// <summary>
    /// 從 ODF MIME 類型取得格式描述。
    /// </summary>
    /// <param name="mimeType">MIME 類型。</param>
    /// <param name="format">成功時傳回格式描述。</param>
    /// <returns>若 MIME 類型已知則傳回 <see langword="true"/>；否則傳回 <see langword="false"/>。</returns>
    public static bool TryGetFormatByMimeType(string? mimeType, out OdfFormatInfo? format)
    {
        format = null;
        return mimeType is not null && MimeTypeFormats.TryGetValue(mimeType, out format);
    }

    /// <summary>
    /// 從檔案名稱或副檔名取得格式描述。
    /// </summary>
    /// <param name="fileName">檔案名稱或副檔名。</param>
    /// <param name="format">成功時傳回格式描述。</param>
    /// <returns>若副檔名已知則傳回 <see langword="true"/>；否則傳回 <see langword="false"/>。</returns>
    public static bool TryGetFormatByFileName(string? fileName, out OdfFormatInfo? format)
    {
        format = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string extension = GetExtension(fileName!);
        return ExtensionFormats.TryGetValue(extension, out format);
    }

    /// <summary>
    /// 從 ODF 文件種類取得格式描述。
    /// </summary>
    /// <param name="kind">ODF 文件種類。</param>
    /// <param name="format">成功時傳回格式描述。</param>
    /// <returns>若文件種類已知則傳回 <see langword="true"/>；否則傳回 <see langword="false"/>。</returns>
    public static bool TryGetFormatByKind(OdfDocumentKind kind, out OdfFormatInfo? format)
    {
        format = FormatTable.FirstOrDefault(item => item.Kind == kind);
        return format is not null;
    }

    private static string GetExtension(string fileName)
    {
        string trimmed = fileName.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) &&
            trimmed.IndexOfAny(new[] { '/', '\\' }) < 0
            ? trimmed
            : Path.GetExtension(trimmed);
    }

    /// <summary>
    /// 將封裝的 ODF 種類轉換為其對應的單一 XML (Flat XML) 種類。
    /// </summary>
    /// <param name="kind">ODF 文件種類</param>
    /// <returns>轉換後的單一 XML 種類；若無對應種類則傳回原種類</returns>
    public static OdfDocumentKind ToFlatKind(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.Text => OdfDocumentKind.FlatText,
            OdfDocumentKind.Spreadsheet => OdfDocumentKind.FlatSpreadsheet,
            OdfDocumentKind.Presentation => OdfDocumentKind.FlatPresentation,
            OdfDocumentKind.Graphics => OdfDocumentKind.FlatGraphics,
            _ => kind
        };
    }

    /// <summary>
    /// 將範本、主控文件及單一 XML 變體轉換為在 <c>office:body</c> 下表示的內容種類。
    /// </summary>
    /// <param name="kind">ODF 文件種類</param>
    /// <returns>對應的內容種類；若無對應種類則傳回原種類</returns>
    public static OdfDocumentKind ToContentKind(OdfDocumentKind kind)
    {
        return kind switch
        {
            OdfDocumentKind.TextTemplate or OdfDocumentKind.TextMaster or OdfDocumentKind.FlatText => OdfDocumentKind.Text,
            OdfDocumentKind.SpreadsheetTemplate or OdfDocumentKind.FlatSpreadsheet => OdfDocumentKind.Spreadsheet,
            OdfDocumentKind.PresentationTemplate or OdfDocumentKind.FlatPresentation => OdfDocumentKind.Presentation,
            OdfDocumentKind.GraphicsTemplate or OdfDocumentKind.FlatGraphics => OdfDocumentKind.Graphics,
            _ => kind
        };
    }

    /// <summary>
    /// 從 <c>office:body</c> 下的第一個 ODF 子元素偵測內容種類。
    /// </summary>
    /// <param name="localName">元素區域名稱</param>
    /// <param name="flat">是否為單一 XML (Flat XML) 文件</param>
    /// <returns>偵測到的 ODF 文件種類</returns>
    public static OdfDocumentKind FromOfficeBodyElement(string? localName, bool flat)
    {
        OdfDocumentKind packagedKind = localName switch
        {
            "text" => OdfDocumentKind.Text,
            "spreadsheet" => OdfDocumentKind.Spreadsheet,
            "presentation" => OdfDocumentKind.Presentation,
            "drawing" => OdfDocumentKind.Graphics,
            "chart" => OdfDocumentKind.Chart,
            "formula" => OdfDocumentKind.Formula,
            "image" => OdfDocumentKind.Image,
            "database" => OdfDocumentKind.Database,
            _ => OdfDocumentKind.Unknown
        };

        return flat ? ToFlatKind(packagedKind) : packagedKind;
    }

    /// <summary>
    /// 傳回宣告的文件種類是否與偵測到的 <c>office:body</c> 種類相容。
    /// </summary>
    /// <param name="declaredKind">宣告的文件種類</param>
    /// <param name="bodyKind">主體元素種類</param>
    /// <returns>若相容或任一種類為未知則傳回 <see langword="true"/>；否則傳回 <see langword="false"/></returns>
    public static bool IsCompatibleWithBodyKind(OdfDocumentKind declaredKind, OdfDocumentKind bodyKind)
    {
        if (declaredKind == OdfDocumentKind.Unknown || bodyKind == OdfDocumentKind.Unknown)
        {
            return true;
        }

        return ToContentKind(declaredKind) == ToContentKind(bodyKind);
    }

    /// <summary>
    /// 傳回指定的種類是否代表單一 XML (Flat XML) ODF 文件。
    /// </summary>
    /// <param name="kind">ODF 文件種類</param>
    /// <returns>若是單一 XML ODF 文件種類則傳回 <see langword="true"/>；否則傳回 <see langword="false"/></returns>
    public static bool IsFlatKind(OdfDocumentKind kind)
    {
        return kind == OdfDocumentKind.FlatText ||
            kind == OdfDocumentKind.FlatSpreadsheet ||
            kind == OdfDocumentKind.FlatPresentation ||
            kind == OdfDocumentKind.FlatGraphics;
    }
}
