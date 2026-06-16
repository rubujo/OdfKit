using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Embedded Documents

    /// <summary>
    /// 取得指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型。</typeparam>
    /// <param name="subPath">封裝中的子路徑。</param>
    /// <returns>嵌入式文件 wrapper。</returns>
    public T GetEmbeddedDocument<T>(string subPath) where T : OdfDocument
    {
        if (string.IsNullOrEmpty(subPath))
            throw new ArgumentException("Subpath cannot be null or empty.", nameof(subPath));
        if (!subPath.EndsWith("/"))
            subPath += "/";

        var ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage), typeof(string) });
        if (ctor != null)
        {
            return (T)ctor.Invoke(new object[] { Package, subPath });
        }
        else
        {
            ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage) });
            if (ctor != null)
            {
                var doc = (T)ctor.Invoke(new object[] { Package });
                doc.SubPath = subPath;
                return doc;
            }
        }
        throw new InvalidOperationException($"Type {typeof(T).Name} does not have a compatible constructor.");
    }

    /// <summary>
    /// 建立指定子路徑的嵌入式 ODF 文件。
    /// </summary>
    /// <typeparam name="T">嵌入式文件 wrapper 類型。</typeparam>
    /// <param name="subPath">封裝中的子路徑。</param>
    /// <returns>建立完成的嵌入式文件 wrapper。</returns>
    public T CreateEmbeddedDocument<T>(string subPath) where T : OdfDocument
    {
        if (string.IsNullOrEmpty(subPath))
            throw new ArgumentException("Subpath cannot be null or empty.", nameof(subPath));
        if (!subPath.EndsWith("/"))
            subPath += "/";

        string mimeType = typeof(T) switch
        {
            Type t when t == typeof(Presentation.PresentationDocument) => "application/vnd.oasis.opendocument.presentation",
            Type t when t == typeof(Spreadsheet.SpreadsheetDocument) => "application/vnd.oasis.opendocument.spreadsheet",
            Type t when t == typeof(OdfKit.Chart.OdfChartDocument) || t == typeof(OdfKit.Chart.ChartDocument) => "application/vnd.oasis.opendocument.chart",
            Type t when t == typeof(OdfKit.Formula.OdfFormulaDocument) || t == typeof(OdfKit.Formula.FormulaDocument) => "application/vnd.oasis.opendocument.formula",
            _ => "application/vnd.oasis.opendocument.text"
        };

        string mimePath = subPath + "mimetype";
        Package.WriteEntry(mimePath, Encoding.UTF8.GetBytes(mimeType), "");

        T doc;
        var ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage), typeof(string) });
        if (ctor != null)
        {
            doc = (T)ctor.Invoke(new object[] { Package, subPath });
        }
        else
        {
            ctor = typeof(T).GetConstructor(new[] { typeof(OdfPackage) });
            if (ctor != null)
            {
                doc = (T)ctor.Invoke(new object[] { Package });
                doc.SubPath = subPath;
            }
            else
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have a compatible constructor.");
            }
        }

        doc.Save();
        return doc;
    }

    #endregion
}
