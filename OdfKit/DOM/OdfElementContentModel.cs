using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// 提供 schema-aware content model 比對與子元素分類的共用輔助方法。
/// </summary>
internal static class OdfElementContentModel
{
    /// <summary>
    /// 依文件順序列舉符合指定 local name 與 namespace URI 的直接子元素。
    /// </summary>
    /// <typeparam name="TElement">要篩選的 typed DOM 元素型別</typeparam>
    /// <param name="parent">父元素</param>
    /// <param name="localName">子元素局部名稱</param>
    /// <param name="namespaceUri">子元素命名空間 URI</param>
    /// <returns>符合條件的直接子元素列舉</returns>
    internal static IEnumerable<TElement> ChildElementsByName<TElement>(
        OdfElement parent,
        string localName,
        string namespaceUri)
        where TElement : OdfElement
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child is TElement typedChild &&
                typedChild.LocalName == localName &&
                typedChild.NamespaceUri == namespaceUri)
            {
                yield return typedChild;
            }
        }
    }

    /// <summary>
    /// 判斷元素是否屬於 <c>office:text</c> 區塊層級 content choice group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為區塊層級內容則為 <see langword="true"/></returns>
    internal static bool IsTextBodyBlockContent(OdfElement element) =>
        element switch
        {
            TextPElement or TextHElement or TextListElement or TextSectionElement or
            TextNumberedParagraphElement or TableTableElement or TextTableOfContentElement or
            TextAlphabeticalIndexElement or TextBibliographyElement or TextIllustrationIndexElement or
            TextObjectIndexElement or TextTableIndexElement or TextUserIndexElement or
            DrawFrameElement or DrawRectElement or DrawLineElement or DrawPathElement or
            DrawCustomShapeElement or TextChangeElement or TextTrackedChangesElement => true,
            _ => false
        };

    /// <summary>
    /// 判斷元素是否屬於 <c>table:table</c> 欄位結構 content choice group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為欄位結構子元素則為 <see langword="true"/></returns>
    internal static bool IsTableColumnStructure(OdfElement element) =>
        element is TableTableColumnGroupElement or TableTableColumnsElement or
        TableTableColumnElement or TableTableHeaderColumnsElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>table:table</c> 列結構 content choice group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為列結構子元素則為 <see langword="true"/></returns>
    internal static bool IsTableRowStructure(OdfElement element) =>
        element is TableTableHeaderRowsElement or TableTableRowsElement or
        TableTableRowElement or TableTableRowGroupElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>draw:page</c> 頁面形狀 content choice group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為頁面形狀子元素則為 <see langword="true"/></returns>
    internal static bool IsDrawPageShapeContent(OdfElement element) =>
        element switch
        {
            DrawFrameElement or DrawRectElement or DrawLineElement or DrawEllipseElement or
            DrawCircleElement or DrawPathElement or DrawPolygonElement or DrawPolylineElement or
            DrawConnectorElement or DrawCustomShapeElement or DrawGElement or DrawPageThumbnailElement or
            DrawCaptionElement or DrawControlElement or DrawMeasureElement or DrawRegularPolygonElement or
            Dr3dSceneElement => true,
            _ => false
        };

    /// <summary>
    /// 判斷元素是否屬於 <c>office:chart</c> 主要 content group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為圖表主要內容則為 <see langword="true"/></returns>
    internal static bool IsChartMainContent(OdfElement element) =>
        element is ChartChartElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>office:image</c> 主要 content group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為影像框架內容則為 <see langword="true"/></returns>
    internal static bool IsImageFrameContent(OdfElement element) =>
        element is DrawFrameElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>office:spreadsheet</c> 主要 content group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為試算表主要內容則為 <see langword="true"/></returns>
    internal static bool IsSpreadsheetTableContent(OdfElement element) =>
        element is TableTableElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>office:database</c> 元件 content group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為資料庫元件子元素則為 <see langword="true"/></returns>
    internal static bool IsDatabaseComponentContent(OdfElement element) =>
        element is DatabaseDataSourceElement or DatabaseFormsElement or DatabaseReportsElement or
        DatabaseQueriesElement or DatabaseTableRepresentationsElement or DatabaseSchemaDefinitionElement;

    /// <summary>
    /// 判斷元素是否屬於 <c>office:presentation</c> 或 <c>office:drawing</c> 主要 content group。
    /// </summary>
    /// <param name="element">要檢查的元素</param>
    /// <returns>若為繪圖頁面主要內容則為 <see langword="true"/></returns>
    internal static bool IsOfficeDrawPageMainContent(OdfElement element) =>
        element is DrawPageElement;
}
