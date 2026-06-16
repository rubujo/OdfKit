using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Document Elements - Fields & Variables

    /// <summary>
    /// 新增一個段落至文件本文結尾。
    /// </summary>
    /// <param name="text">段落的文字內容</param>
    /// <returns>新建立的段落執行個體</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            BodyTextRoot.AppendChild(startNode);
            BodyTextRoot.AppendChild(pNode);
            BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            BodyTextRoot.AppendChild(pNode);
        }
        return new OdfParagraph(pNode, this);
    }

    /// <summary>
    /// 新增一個標題至文件本文結尾。
    /// </summary>
    /// <param name="text">標題的文字內容</param>
    /// <param name="outlineLevel">標題的大綱階層</param>
    /// <returns>新建立的標題執行個體</returns>
    public OdfHeading AddHeading(string text, int outlineLevel)
    {
        var hNode = OdfNodeFactory.CreateElement("h", OdfNamespaces.Text, "text");
        hNode.TextContent = text;
        hNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        if (TrackedChanges)
        {
            string changeId = RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            BodyTextRoot.AppendChild(startNode);
            BodyTextRoot.AppendChild(hNode);
            BodyTextRoot.AppendChild(endNode);
        }
        else
        {
            BodyTextRoot.AppendChild(hNode);
        }
        return new OdfHeading(hNode, this);
    }

    /// <summary>
    /// 新增一個項目清單至文件本文結尾。
    /// </summary>
    /// <param name="styleName">項目清單樣式名稱</param>
    /// <returns>新建立的清單項目</returns>
    public OdfList AddList(string? styleName = null)
    {
        var listNode = OdfNodeFactory.CreateElement("list", OdfNamespaces.Text, "text");
        if (styleName is not null)
        {
            listNode.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
        }
        BodyTextRoot.AppendChild(listNode);
        return new OdfList(listNode, this);
    }

    /// <summary>
    /// 以多層級樣式定義建立清單，樣式寫入 styles.xml 的 office:styles 區段。
    /// </summary>
    /// <param name="styleName">清單樣式名稱，必須唯一。</param>
    /// <param name="levels">各層級的樣式設定；Level 屬性需從 1 開始連續遞增。</param>
    /// <returns>新建立的清單（已套用樣式名稱）。</returns>
    public OdfList AddListWithStyle(string styleName, IReadOnlyList<OdfListLevelStyle> levels)
    {
        var officeStyles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
        var listStyleNode = OdfNodeFactory.CreateElement("list-style", OdfNamespaces.Text, "text");
        listStyleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        foreach (var lvl in levels)
        {
            OdfNode levelNode;
            if (lvl.Type == OdfListLevelType.Bullet)
            {
                levelNode = OdfNodeFactory.CreateElement("list-level-style-bullet", OdfNamespaces.Text, "text");
                levelNode.SetAttribute("bullet-char", OdfNamespaces.Text, lvl.BulletChar ?? "•", "text");
            }
            else
            {
                levelNode = OdfNodeFactory.CreateElement("list-level-style-number", OdfNamespaces.Text, "text");
                levelNode.SetAttribute("num-format", OdfNamespaces.Fo, lvl.NumFormat, "fo");
                if (!string.IsNullOrEmpty(lvl.NumPrefix))
                    levelNode.SetAttribute("num-prefix", OdfNamespaces.Text, lvl.NumPrefix!, "text");
                if (lvl.NumSuffix is not null)
                    levelNode.SetAttribute("num-suffix", OdfNamespaces.Text, lvl.NumSuffix, "text");
            }
            levelNode.SetAttribute("level", OdfNamespaces.Text, lvl.Level.ToString(), "text");

            var propsNode = OdfNodeFactory.CreateElement("list-level-properties", OdfNamespaces.Style, "style");
            var alignNode = OdfNodeFactory.CreateElement("list-level-label-alignment", OdfNamespaces.Style, "style");
            alignNode.SetAttribute("label-followed-by", OdfNamespaces.Text, "listtab", "text");
            if (lvl.IndentLeft.Value > 0)
                alignNode.SetAttribute("margin-left", OdfNamespaces.Fo, lvl.IndentLeft.ToString(), "fo");
            if (lvl.FirstLineIndent.Value != 0)
                alignNode.SetAttribute("text-indent", OdfNamespaces.Fo, lvl.FirstLineIndent.ToString(), "fo");
            propsNode.AppendChild(alignNode);
            levelNode.AppendChild(propsNode);

            listStyleNode.AppendChild(levelNode);
        }

        officeStyles.AppendChild(listStyleNode);
        return AddList(styleName);
    }

    /// <summary>
    /// 在指定的段落中新增日期欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddDateField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("date", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增時間欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddTimeField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("time", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增作者名稱欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddAuthorField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("author-name", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增章節欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    internal void AddChapterField(OdfParagraph paragraph)
    {
        var fNode = OdfNodeFactory.CreateElement("chapter", OdfNamespaces.Text, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增序號欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">序號欄位的名稱</param>
    /// <param name="numFormat">序號的編號格式</param>
    internal void AddSequenceField(OdfParagraph paragraph, string name, string numFormat = "1")
    {
        var fNode = OdfNodeFactory.CreateElement("sequence", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.SetAttribute("num-format", OdfNamespaces.Style, numFormat, "style");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增參考項目欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="refName">要參考的項目名稱</param>
    internal void AddReferenceField(OdfParagraph paragraph, string refName)
    {
        var fNode = OdfNodeFactory.CreateElement("reference-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, refName, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增序號交互參照欄位 (<c>text:sequence-ref</c>)。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="sequenceName">序號欄位名稱（需與 AddSequenceField 使用的 name 相同）</param>
    /// <param name="referenceFormat">參照格式，預設為 "value"（顯示數值）</param>
    internal void AddSequenceRefField(OdfParagraph paragraph, string sequenceName, string referenceFormat = "value")
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(sequenceName))
            throw new ArgumentException("序號欄位名稱不可為空。", nameof(sequenceName));
        var fNode = OdfNodeFactory.CreateElement("sequence-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, sequenceName, "text");
        fNode.SetAttribute("reference-format", OdfNamespaces.Text, referenceFormat, "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增書籤參照欄位。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體。</param>
    /// <param name="bookmarkName">要參照的書籤名稱。</param>
    /// <param name="referenceFormat">參照格式，預設為 "text"。</param>
    internal void AddBookmarkReferenceField(OdfParagraph paragraph, string bookmarkName, string referenceFormat = "text")
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(bookmarkName))
            throw new ArgumentException("書籤名稱不可為空。", nameof(bookmarkName));

        var fNode = OdfNodeFactory.CreateElement("bookmark-ref", OdfNamespaces.Text, "text");
        fNode.SetAttribute("ref-name", OdfNamespaces.Text, bookmarkName, "text");
        fNode.SetAttribute("reference-format", OdfNamespaces.Text, referenceFormat, "text");
        fNode.TextContent = bookmarkName; // 預設顯示書籤名稱
        paragraph.Node.AppendChild(fNode);
    }


    /// <summary>
    /// 在指定的段落中設定變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    /// <param name="value">變數的值</param>
    internal void AddVariableSetField(OdfParagraph paragraph, string name, string value)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-set", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        fNode.TextContent = value;
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中取得變數欄位值。
    /// </summary>
    /// <param name="paragraph">要新增欄位的段落執行個體</param>
    /// <param name="name">變數的名稱</param>
    internal void AddVariableGetField(OdfParagraph paragraph, string name)
    {
        var fNode = OdfNodeFactory.CreateElement("variable-get", OdfNamespaces.Text, "text");
        fNode.SetAttribute("name", OdfNamespaces.Text, name, "text");
        paragraph.Node.AppendChild(fNode);
    }

    #endregion
}
