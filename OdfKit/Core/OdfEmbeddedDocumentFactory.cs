using OdfKit.Chart;
using OdfKit.Database;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit.Core;

/// <summary>
/// 嵌入式 ODF 文件類型的編譯期工廠註冊表，避免 <see cref="OdfDocument.GetEmbeddedDocument{T}"/> 依賴反射（PERF-5e）。
/// </summary>
internal static class OdfEmbeddedDocumentFactory
{
    private static readonly Dictionary<Type, Func<OdfPackage, string, OdfDocument>> WithSubPathFactories = new();
    private static readonly Dictionary<Type, Func<OdfPackage, OdfDocument>> PackageOnlyFactories = new();
    private static readonly Dictionary<Type, string> EmbeddedMimeTypes = new();

    static OdfEmbeddedDocumentFactory()
    {
        RegisterWithSubPath<OdfChartDocument>((package, subPath) => new OdfChartDocument(package, subPath));
        RegisterWithSubPath<ChartDocument>((package, subPath) => new ChartDocument(package, subPath));
        RegisterWithSubPath<FlatChartDocument>((package, subPath) => new FlatChartDocument(package, subPath));

        RegisterWithSubPath<OdfFormulaDocument>((package, subPath) => new OdfFormulaDocument(package, subPath));
        RegisterWithSubPath<FormulaDocument>((package, subPath) => new FormulaDocument(package, subPath));
        RegisterWithSubPath<FlatFormulaDocument>((package, subPath) => new FlatFormulaDocument(package, subPath));

        RegisterPackageOnly<SpreadsheetDocument>(package => new SpreadsheetDocument(package), "application/vnd.oasis.opendocument.spreadsheet");
        RegisterPackageOnly<FlatSpreadsheetDocument>(package => new FlatSpreadsheetDocument(package), "application/vnd.oasis.opendocument.spreadsheet");
        RegisterPackageOnly<SpreadsheetTemplateDocument>(package => new SpreadsheetTemplateDocument(package), "application/vnd.oasis.opendocument.spreadsheet-template");

        RegisterPackageOnly<PresentationDocument>(package => new PresentationDocument(package), "application/vnd.oasis.opendocument.presentation");
        RegisterPackageOnly<FlatPresentationDocument>(package => new FlatPresentationDocument(package), "application/vnd.oasis.opendocument.presentation");
        RegisterPackageOnly<PresentationTemplateDocument>(package => new PresentationTemplateDocument(package), "application/vnd.oasis.opendocument.presentation-template");

        RegisterPackageOnly<TextDocument>(package => new TextDocument(package), "application/vnd.oasis.opendocument.text");
        RegisterPackageOnly<FlatTextDocument>(package => new FlatTextDocument(package), "application/vnd.oasis.opendocument.text");
        RegisterPackageOnly<TextTemplateDocument>(package => new TextTemplateDocument(package), "application/vnd.oasis.opendocument.text-template");
        RegisterPackageOnly<TextMasterDocument>(package => new TextMasterDocument(package), "application/vnd.oasis.opendocument.text-master");

        RegisterPackageOnly<DrawingDocument>(package => new DrawingDocument(package), "application/vnd.oasis.opendocument.graphics");
        RegisterPackageOnly<FlatGraphicsDocument>(package => new FlatGraphicsDocument(package), "application/vnd.oasis.opendocument.graphics");
        RegisterPackageOnly<GraphicsTemplateDocument>(package => new GraphicsTemplateDocument(package), "application/vnd.oasis.opendocument.graphics-template");

        RegisterPackageOnly<OdfImageDocument>(package => new OdfImageDocument(package), "application/vnd.oasis.opendocument.image");
        RegisterPackageOnly<FlatImageDocument>(package => new FlatImageDocument(package), "application/vnd.oasis.opendocument.image");

        RegisterPackageOnly<OdfDatabaseDocument>(package => new OdfDatabaseDocument(package), "application/vnd.oasis.opendocument.base");

        EmbeddedMimeTypes[typeof(OdfChartDocument)] = "application/vnd.oasis.opendocument.chart";
        EmbeddedMimeTypes[typeof(ChartDocument)] = "application/vnd.oasis.opendocument.chart";
        EmbeddedMimeTypes[typeof(FlatChartDocument)] = "application/vnd.oasis.opendocument.chart";
        EmbeddedMimeTypes[typeof(OdfFormulaDocument)] = "application/vnd.oasis.opendocument.formula";
        EmbeddedMimeTypes[typeof(FormulaDocument)] = "application/vnd.oasis.opendocument.formula";
        EmbeddedMimeTypes[typeof(FlatFormulaDocument)] = "application/vnd.oasis.opendocument.formula";
    }

    /// <summary>
    /// 嘗試以註冊工廠建立嵌入式文件執行個體。
    /// </summary>
    internal static bool TryCreate<T>(OdfPackage package, string subPath, out T document) where T : OdfDocument
    {
        Type type = typeof(T);
        if (WithSubPathFactories.TryGetValue(type, out Func<OdfPackage, string, OdfDocument>? withSubPath))
        {
            document = (T)withSubPath(package, subPath);
            return true;
        }

        if (PackageOnlyFactories.TryGetValue(type, out Func<OdfPackage, OdfDocument>? packageOnly))
        {
            document = (T)packageOnly(package);
            document.SubPath = subPath;
            return true;
        }

        document = null!;
        return false;
    }

    /// <summary>
    /// 取得嵌入式文件類型對應的 ODF mimetype。
    /// </summary>
    internal static string GetMimeType<T>() where T : OdfDocument
    {
        if (EmbeddedMimeTypes.TryGetValue(typeof(T), out string? mimeType))
        {
            return mimeType;
        }

        return "application/vnd.oasis.opendocument.text";
    }

    private static void RegisterWithSubPath<T>(Func<OdfPackage, string, T> factory) where T : OdfDocument
    {
        WithSubPathFactories[typeof(T)] = (package, subPath) => factory(package, subPath);
    }

    private static void RegisterPackageOnly<T>(Func<OdfPackage, T> factory, string mimeType) where T : OdfDocument
    {
        PackageOnlyFactories[typeof(T)] = package => factory(package);
        EmbeddedMimeTypes[typeof(T)] = mimeType;
    }
}
