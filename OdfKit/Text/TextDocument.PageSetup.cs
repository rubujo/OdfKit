using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Page Setup & Mirrored Layouts


    /// <summary>
    /// 取得預設的頁面設定。
    /// </summary>
    /// <returns>預設頁面設定物件</returns>
    public OdfPageSetup GetDefaultPageSetup()
    {
        return new OdfPageSetup(this);
    }

    /// <summary>
    /// 新增一個具名頁面樣式（master-page + page-layout），並可選擇性地配置其設定。
    /// </summary>
    /// <param name="name">主頁面樣式名稱（例如 "Landscape"）</param>
    /// <param name="configure">可選的頁面設定回呼</param>
    public OdfPageStyle AddPageStyle(string name, Action<OdfPageSetup>? configure = null)
    {
        string layoutName = $"MPL_{name}";
        var setup = new OdfPageSetup(this, name, layoutName);
        setup.EnsureNodes();
        configure?.Invoke(setup);
        return new OdfPageStyle(name);
    }

    /// <summary>
    /// 取得所有已定義的主頁面樣式名稱清單。
    /// </summary>
    public IReadOnlyList<string> GetPageStyleNames()
    {
        var masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");
        var names = new List<string>();
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
            {
                string? n = child.GetAttribute("name", OdfNamespaces.Style);
                if (!string.IsNullOrEmpty(n))
                    names.Add(n!);
            }
        }
        return names;
    }


    #endregion
}
