using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OfficeTextElement API.
/// 為 <see cref="OfficeTextElement"/> 提供 <c>office:text</c> 區塊層級 content model facade。
/// </summary>
public partial class OfficeTextElement
{
    /// <summary>
    /// Provides the member member.
    /// 依文件順序列舉 <c>office:text</c> 區塊層級 content choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> BlockContentChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsTextBodyBlockContent(element))
                {
                    yield return element;
                }
            }
        }
    }
    /// <summary>
    /// Short overload of AppendParagraph that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AppendParagraph 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public TextPElement AppendParagraph() => AppendParagraph("");


    /// <summary>
    /// Appends paragraph.
    /// 在 <c>office:text</c> 末尾新增段落。
    /// </summary>
    /// <param name="text">段落文字內容</param>
    /// <returns>新增的 <c>text:p</c> 元素</returns>
    public TextPElement AppendParagraph(string text)
    {
        TextPElement paragraph = AppendElement(new TextPElement("text"));
        if (!string.IsNullOrEmpty(text))
        {
            paragraph.TextContent = text;
        }

        return paragraph;
    }

    /// <summary>
    /// Short overload of AppendHeading that accepts text; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 text；其餘可選參數使用預設值並轉呼叫最長 AppendHeading 多載。
    /// </summary>
    public TextHElement AppendHeading(string text) => AppendHeading(text, 1);


    /// <summary>
    /// Appends heading.
    /// 在 <c>office:text</c> 末尾新增標題。
    /// </summary>
    /// <param name="text">標題文字內容</param>
    /// <param name="outlineLevel">大綱階層，預設為 1</param>
    /// <returns>新增的 <c>text:h</c> 元素</returns>
    public TextHElement AppendHeading(string text, int outlineLevel)
    {
        TextHElement heading = AppendElement(new TextHElement("text"));
        heading.OutlineLevel = outlineLevel;
        heading.TextContent = text;
        return heading;
    }

    /// <summary>
    /// Short overload of AppendList that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AppendList 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public TextListElement AppendList() => AppendList(null);


    /// <summary>
    /// Appends list.
    /// 在 <c>office:text</c> 末尾新增專案清單。
    /// </summary>
    /// <param name="styleName">選用的清單樣式名稱</param>
    /// <returns>新增的 <c>text:list</c> 元素</returns>
    public TextListElement AppendList(string? styleName)
    {
        TextListElement list = AppendElement(new TextListElement("text"));
        if (styleName is not null)
        {
            list.StyleName = styleName;
        }

        return list;
    }

    /// <summary>
    /// Short overload of AppendTable that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：AppendTable 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public TableTableElement AppendTable() => AppendTable(null);


    /// <summary>
    /// Appends table.
    /// 在 <c>office:text</c> 末尾新增表格。
    /// </summary>
    /// <param name="name">選用的表格名稱</param>
    /// <returns>新增的 <c>table:table</c> 元素</returns>
    public TableTableElement AppendTable(string? name)
    {
        TableTableElement table = AppendElement(new TableTableElement("table"));
        if (name is not null)
        {
            table.Name = name;
        }

        return table;
    }


    /// <summary>
    /// Appends section.
    /// 在 <c>office:text</c> 末尾新增章節。
    /// </summary>
    /// <param name="name">章節名稱</param>
    /// <returns>新增的 <c>text:section</c> 元素</returns>
    public TextSectionElement AppendSection(string name)
    {
        TextSectionElement section = AppendElement(new TextSectionElement("text"));
        section.Name = name;
        return section;
    }
}
