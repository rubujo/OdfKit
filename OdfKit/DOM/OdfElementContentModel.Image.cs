using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OfficeImageElement API.
/// 為 <see cref="OfficeImageElement"/> 提供 <c>office:image</c> content model facade。
/// </summary>
public partial class OfficeImageElement
{
    /// <summary>
    /// Provides the member member.
    /// 依文件順序列舉 <c>office:image</c> 影像框架 content group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> ImageFrameChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsImageFrameContent(element))
                {
                    yield return element;
                }
            }
        }
    }
    /// <summary>
    /// Short overload of AppendImageFrame that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AppendImageFrame 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public DrawFrameElement AppendImageFrame() => AppendImageFrame(null);


    /// <summary>
    /// Appends image frame.
    /// 在 <c>office:image</c> 末尾新增影像框架。
    /// </summary>
    /// <param name="name">選用的框架名稱</param>
    /// <returns>新增的 <c>draw:frame</c> 元素</returns>
    public DrawFrameElement AppendImageFrame(string? name)
    {
        DrawFrameElement frame = AppendElement(new DrawFrameElement("draw"));
        if (name is not null)
        {
            frame.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }

        return frame;
    }

}
