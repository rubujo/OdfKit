using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="DrawPageElement"/> 提供 <c>draw:page</c> 頁面 content model facade。
/// </summary>
public partial class DrawPageElement
{
    /// <summary>
    /// 依文件順序列舉 <c>draw:page</c> 形狀 content choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> ShapeContentChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsDrawPageShapeContent(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 依文件順序列舉 <c>draw:page</c> 中非形狀附註類直接子元素（例如 <c>presentation:notes</c>）。
    /// </summary>
    public IEnumerable<OdfElement> PageAnnotationChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is PresentationNotesElement notes)
                {
                    yield return notes;
                }
            }
        }
    }

    /// <summary>
    /// 在 <c>draw:page</c> 末尾新增框架。
    /// </summary>
    /// <param name="name">選用的框架名稱</param>
    /// <returns>新增的 <c>draw:frame</c> 元素</returns>
    public DrawFrameElement AppendFrame(string? name = null)
    {
        DrawFrameElement frame = AppendElement(new DrawFrameElement("draw"));
        if (name is not null)
        {
            frame.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }

        return frame;
    }

    /// <summary>
    /// 在 <c>draw:page</c> 末尾新增矩形。
    /// </summary>
    /// <param name="name">選用的形狀名稱</param>
    /// <returns>新增的 <c>draw:rect</c> 元素</returns>
    public DrawRectElement AppendRectangle(string? name = null)
    {
        DrawRectElement rectangle = AppendElement(new DrawRectElement("draw"));
        if (name is not null)
        {
            rectangle.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }

        return rectangle;
    }

    /// <summary>
    /// 在 <c>draw:page</c> 末尾新增簡報備忘稿。
    /// </summary>
    /// <returns>新增的 <c>presentation:notes</c> 元素</returns>
    public PresentationNotesElement AppendNotes()
    {
        return AppendElement(new PresentationNotesElement("presentation"));
    }
}
