using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region TOC (Table of Contents)


    /// <summary>
    /// 新增目錄項目至文件本文結尾。
    /// </summary>
    /// <param name="title">目錄標題</param>
    /// <param name="outlineLevel">目錄的大綱階層上限</param>
    /// <returns>新建立的目錄物件</returns>
    public OdfTableOfContents AddTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
    {
        var tocNode = OdfNodeFactory.CreateElement("table-of-content", OdfNamespaces.Text, "text");
        tocNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        var sourceNode = OdfNodeFactory.CreateElement("table-of-content-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(), "text");
        tocNode.AppendChild(sourceNode);

        var bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");

        var titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);

        tocNode.AppendChild(bodyNode);
        BodyTextRoot.AppendChild(tocNode);

        SetUpdateFieldsWhenOpening(true);
        return new OdfTableOfContents(tocNode, this);
    }

    private void SetUpdateFieldsWhenOpening(bool update)
    {
        var sc = FindOrCreateSettingsNode(SettingsDom, "view-settings");
        var map = FindOrCreateMapNode(sc, "Views");
        var entry = FindOrCreateMapEntryNode(map);
        var item = FindOrCreateConfigItemNode(entry, "UpdateFieldsWhenOpening", "boolean");
        item.TextContent = update ? "true" : "false";
    }


    #endregion
}
