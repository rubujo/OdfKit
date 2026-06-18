using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="OfficeDrawingElement"/> 提供 <c>office:drawing</c> content model facade。
/// </summary>
public partial class OfficeDrawingElement
{
    /// <summary>
    /// 依文件順序列舉 <c>office:drawing</c> 主要 content group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> DrawingPageChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsOfficeDrawPageMainContent(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 在 <c>office:drawing</c> 主要 content 區段新增繪圖頁面。
    /// </summary>
    /// <param name="name">選用的頁面名稱。</param>
    /// <returns>新增的 <c>draw:page</c> 元素。</returns>
    public DrawPageElement AppendPage(string? name = null)
    {
        DrawPageElement page = new("draw");
        if (name is not null)
        {
            page.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }

        return InsertDrawingPage(page);
    }

    private DrawPageElement InsertDrawingPage(DrawPageElement page)
    {
        DrawPageElement? lastPage = DrawPageChildElements.LastOrDefault();
        if (lastPage is not null)
        {
            return InsertElementAfter(page, lastPage);
        }

        return AppendElement(page);
    }
}
