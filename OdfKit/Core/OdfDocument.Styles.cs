using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    /// <summary>
    /// 取得文件中所有的主控頁面（Master Page）。
    /// </summary>
    /// <returns>主控頁面集合。</returns>
    public IEnumerable<OdfMasterPage> GetMasterPages()
    {
        var masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
            {
                yield return new OdfMasterPage(child);
            }
        }
    }

    /// <summary>
    /// 新增一個指定名稱的主控頁面（Master Page）。
    /// </summary>
    /// <param name="name">主控頁面的名稱。</param>
    /// <returns>新增的主控頁面執行個體。</returns>
    public OdfMasterPage AddMasterPage(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDocument_MainCannotBeEmpty"), nameof(name));

        var masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");

        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == name)
            {
                return new OdfMasterPage(child);
            }
        }

        var masterPageNode = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPageNode.SetAttribute("name", OdfNamespaces.Style, name, "style");
        masterPageNode.SetAttribute("page-layout-name", OdfNamespaces.Style, "Standard", "style");
        masterStyles.AppendChild(masterPageNode);

        return new OdfMasterPage(masterPageNode);
    }
}
