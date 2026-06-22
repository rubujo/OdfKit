using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="OfficeChartElement"/> 提供 <c>office:chart</c> content model facade。
/// </summary>
public partial class OfficeChartElement
{
    /// <summary>
    /// 依文件順序列舉 <c>office:chart</c> 主要 content group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> ChartMainChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsChartMainContent(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 在 <c>office:chart</c> 末尾新增圖表根節點。
    /// </summary>
    /// <returns>新增的 <c>chart:chart</c> 元素</returns>
    public ChartChartElement AppendChart()
    {
        return AppendElement(new ChartChartElement("chart"));
    }
}
