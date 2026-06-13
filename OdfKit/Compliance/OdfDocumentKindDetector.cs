#pragma warning restore CS1591
using System;
using System.Collections.Generic;
using System.IO;

namespace OdfKit.Compliance;

/// <summary>
/// 從 MIME 類型與副檔名偵測 ODF 文件種類。
/// </summary>
public static class OdfDocumentKindDetector
{
    private static readonly Dictionary<string, OdfDocumentKind> MimeTypeKinds = new(StringComparer.Ordinal)
    {
        ["application/vnd.oasis.opendocument.text"] = OdfDocumentKind.Text,
        ["application/vnd.oasis.opendocument.text-template"] = OdfDocumentKind.TextTemplate,
        ["application/vnd.oasis.opendocument.text-master"] = OdfDocumentKind.TextMaster,
        ["application/vnd.oasis.opendocument.spreadsheet"] = OdfDocumentKind.Spreadsheet,
        ["application/vnd.oasis.opendocument.spreadsheet-template"] = OdfDocumentKind.SpreadsheetTemplate,
        ["application/vnd.oasis.opendocument.presentation"] = OdfDocumentKind.Presentation,
        ["application/vnd.oasis.opendocument.presentation-template"] = OdfDocumentKind.PresentationTemplate,
        ["application/vnd.oasis.opendocument.graphics"] = OdfDocumentKind.Graphics,
        ["application/vnd.oasis.opendocument.graphics-template"] = OdfDocumentKind.GraphicsTemplate,
        ["application/vnd.oasis.opendocument.chart"] = OdfDocumentKind.Chart,
        ["application/vnd.oasis.opendocument.formula"] = OdfDocumentKind.Formula,
        ["application/vnd.oasis.opendocument.image"] = OdfDocumentKind.Image,
        ["application/vnd.oasis.opendocument.database"] = OdfDocumentKind.Database
    };

    private static readonly Dictionary<string, OdfDocumentKind> ExtensionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".odt"] = OdfDocumentKind.Text,
        [".ott"] = OdfDocumentKind.TextTemplate,
        [".odm"] = OdfDocumentKind.TextMaster,
        [".ods"] = OdfDocumentKind.Spreadsheet,
        [".ots"] = OdfDocumentKind.SpreadsheetTemplate,
        [".odp"] = OdfDocumentKind.Presentation,
        [".otp"] = OdfDocumentKind.PresentationTemplate,
        [".odg"] = OdfDocumentKind.Graphics,
        [".otg"] = OdfDocumentKind.GraphicsTemplate,
        [".odc"] = OdfDocumentKind.Chart,
        [".odf"] = OdfDocumentKind.Formula,
        [".odi"] = OdfDocumentKind.Image,
        [".odb"] = OdfDocumentKind.Database,
        [".fodt"] = OdfDocumentKind.FlatText,
        [".fods"] = OdfDocumentKind.FlatSpreadsheet,
        [".fodp"] = OdfDocumentKind.FlatPresentation,
        [".fodg"] = OdfDocumentKind.FlatGraphics
    };

    /// <summary>
    /// 從 ODF MIME 類型偵測封裝的 ODF 文件種類。
    /// </summary>
    /// <param name="mimeType">MIME 類型</param>
    /// <returns>偵測到的 ODF 文件種類；若無法識別則傳回 <see cref="OdfDocumentKind.Unknown"/></returns>
    public static OdfDocumentKind FromMimeType(string? mimeType)
    {
        return mimeType is not null && MimeTypeKinds.TryGetValue(mimeType, out OdfDocumentKind kind)
            ? kind
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

        string extension = Path.GetExtension(fileName);
        return ExtensionKinds.TryGetValue(extension, out OdfDocumentKind kind)
            ? kind
            : OdfDocumentKind.Unknown;
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
