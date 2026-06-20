using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 資料庫的表單與報表設計器，用於建模表單屬性、常用控制項與報表結構。
/// </summary>
public sealed class OdfDatabaseFormDesigner
{
    private const string FormNamespace = "urn:oasis:names:tc:opendocument:xmlns:form:1.0";
    private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const string DrawNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private const string SvgNamespace = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private const string ReportNamespace = "urn:oasis:names:tc:opendocument:xmlns:report:1.0";

    private readonly OdfDocument _document;
    private readonly OdfNode _formNode;
    private int _controlCounter = 1;

    /// <summary>
    /// 初始化 <see cref="OdfDatabaseFormDesigner"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">要進行表單設計的 ODF 子文件。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="document"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfDatabaseFormDesigner(OdfDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));

        var body = FindOrCreateChild(_document.ContentDom, "body", OfficeNamespace, "office");
        var text = FindOrCreateChild(body, "text", OfficeNamespace, "office");
        _formNode = FindOrCreateChild(text, "form", FormNamespace, "form");
        if (string.IsNullOrEmpty(_formNode.GetAttribute("name", FormNamespace)))
        {
            _formNode.SetAttribute("name", FormNamespace, "Standard", "form");
        }
    }

    /// <summary>
    /// 新增文字框控制項（form:text）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="defaultValue">預設文字內容。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的文字框 OdfNode 節點。</returns>
    public OdfNode AddTextBox(
        string name,
        string label,
        string? defaultValue = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var textNode = OdfNodeFactory.CreateElement("text", FormNamespace, "form");
        textNode.SetAttribute("name", FormNamespace, name, "form");
        textNode.SetAttribute("id", FormNamespace, id, "form");

        if (!string.IsNullOrEmpty(defaultValue) || !string.IsNullOrEmpty(label))
        {
            var props = OdfNodeFactory.CreateElement("properties", FormNamespace, "form");
            textNode.AppendChild(props);

            if (!string.IsNullOrEmpty(defaultValue))
            {
                var prop = OdfNodeFactory.CreateElement("property", FormNamespace, "form");
                prop.SetAttribute("property-name", FormNamespace, "DefaultVal", "form");
                prop.SetAttribute("value-type", OfficeNamespace, "string", "office");
                prop.SetAttribute("string-value", FormNamespace, defaultValue!, "form");
                props.AppendChild(prop);
            }

            if (!string.IsNullOrEmpty(label))
            {
                var prop = OdfNodeFactory.CreateElement("property", FormNamespace, "form");
                prop.SetAttribute("property-name", FormNamespace, "Label", "form");
                prop.SetAttribute("value-type", OfficeNamespace, "string", "office");
                prop.SetAttribute("string-value", FormNamespace, label, "form");
                props.AppendChild(prop);
            }
        }

        _formNode.AppendChild(textNode);
        AddDrawControl(id, x, y, width, height);
        return textNode;
    }

    /// <summary>
    /// 新增核取方塊控制項（form:checkbox）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">顯示的標籤文字。</param>
    /// <param name="isChecked">預設勾選狀態。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的核取方塊 OdfNode 節點。</returns>
    public OdfNode AddCheckBox(
        string name,
        string label,
        bool isChecked = false,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var checkNode = OdfNodeFactory.CreateElement("checkbox", FormNamespace, "form");
        checkNode.SetAttribute("name", FormNamespace, name, "form");
        checkNode.SetAttribute("id", FormNamespace, id, "form");
        checkNode.SetAttribute("label", FormNamespace, label, "form");
        checkNode.SetAttribute("current-state", FormNamespace, isChecked ? "checked" : "unchecked", "form");

        _formNode.AppendChild(checkNode);
        AddDrawControl(id, x, y, width, height);
        return checkNode;
    }

    /// <summary>
    /// 新增下拉式清單控制項（form:listbox）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="items">下拉選單的選項清單。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的下拉選單 OdfNode 節點。</returns>
    public OdfNode AddListBox(
        string name,
        string label,
        IEnumerable<string>? items = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var listNode = OdfNodeFactory.CreateElement("listbox", FormNamespace, "form");
        listNode.SetAttribute("name", FormNamespace, name, "form");
        listNode.SetAttribute("id", FormNamespace, id, "form");

        if (!string.IsNullOrEmpty(label))
        {
            var props = OdfNodeFactory.CreateElement("properties", FormNamespace, "form");
            listNode.AppendChild(props);

            var prop = OdfNodeFactory.CreateElement("property", FormNamespace, "form");
            prop.SetAttribute("property-name", FormNamespace, "Label", "form");
            prop.SetAttribute("value-type", OfficeNamespace, "string", "office");
            prop.SetAttribute("string-value", FormNamespace, label, "form");
            props.AppendChild(prop);
        }

        if (items is not null)
        {
            foreach (var item in items)
            {
                var option = OdfNodeFactory.CreateElement("option", FormNamespace, "form");
                option.SetAttribute("value", FormNamespace, item, "form");
                listNode.AppendChild(option);
            }
        }

        _formNode.AppendChild(listNode);
        AddDrawControl(id, x, y, width, height);
        return listNode;
    }

    /// <summary>
    /// 新增按鈕控制項（form:button）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">按鈕上顯示的文字。</param>
    /// <param name="value">按鈕的值或動作命令。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的按鈕 OdfNode 節點。</returns>
    public OdfNode AddButton(
        string name,
        string label,
        string? value = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var buttonNode = OdfNodeFactory.CreateElement("button", FormNamespace, "form");
        buttonNode.SetAttribute("name", FormNamespace, name, "form");
        buttonNode.SetAttribute("id", FormNamespace, id, "form");
        buttonNode.SetAttribute("label", FormNamespace, label, "form");
        if (!string.IsNullOrEmpty(value))
        {
            buttonNode.SetAttribute("value", FormNamespace, value!, "form");
        }

        _formNode.AppendChild(buttonNode);
        AddDrawControl(id, x, y, width, height);
        return buttonNode;
    }

    /// <summary>
    /// 新增文字標籤控制項（form:fixed-text）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤顯示的文字。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的標籤 OdfNode 節點。</returns>
    public OdfNode AddLabel(
        string name,
        string label,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var labelNode = OdfNodeFactory.CreateElement("fixed-text", FormNamespace, "form");
        labelNode.SetAttribute("name", FormNamespace, name, "form");
        labelNode.SetAttribute("id", FormNamespace, id, "form");
        labelNode.SetAttribute("label", FormNamespace, label, "form");

        _formNode.AppendChild(labelNode);
        AddDrawControl(id, x, y, width, height);
        return labelNode;
    }

    /// <summary>
    /// 定義報表結構，建立頁首、細節與群組首等區段。
    /// </summary>
    /// <param name="hasPageHeader">是否包含頁首區段。</param>
    /// <param name="hasDetail">是否包含資料明細區段。</param>
    /// <param name="hasGroupHeader">是否包含群組首區段。</param>
    /// <param name="groupName">群組區段的識別名稱。</param>
    public void DefineReportStructure(bool hasPageHeader, bool hasDetail, bool hasGroupHeader, string? groupName = null)
    {
        var body = FindOrCreateChild(_document.ContentDom, "body", OfficeNamespace, "office");
        var report = FindOrCreateChild(body, "report", ReportNamespace, "report");

        if (hasPageHeader)
        {
            FindOrCreateChild(report, "page-header", ReportNamespace, "report");
        }

        if (hasGroupHeader && !string.IsNullOrEmpty(groupName))
        {
            var groups = FindOrCreateChild(report, "groups", ReportNamespace, "report");
            var group = OdfNodeFactory.CreateElement("group", ReportNamespace, "report");
            group.SetAttribute("name", ReportNamespace, groupName!, "report");
            FindOrCreateChild(group, "group-header", ReportNamespace, "report");
            groups.AppendChild(group);
        }

        if (hasDetail)
        {
            FindOrCreateChild(report, "detail", ReportNamespace, "report");
        }
    }

    private void AddDrawControl(string controlId, OdfLength? x, OdfLength? y, OdfLength? width, OdfLength? height)
    {
        var body = FindOrCreateChild(_document.ContentDom, "body", OfficeNamespace, "office");
        var text = FindOrCreateChild(body, "text", OfficeNamespace, "office");

        // 建立或尋找一個存放 draw 的段落
        var p = FindOrCreateChild(text, "p", TextNamespace, "text");

        var drawControl = OdfNodeFactory.CreateElement("control", DrawNamespace, "draw");
        drawControl.SetAttribute("control", DrawNamespace, controlId, "draw");

        if (x.HasValue)
        {
            drawControl.SetAttribute("x", SvgNamespace, x.Value.ToString()!, "svg");
        }
        if (y.HasValue)
        {
            drawControl.SetAttribute("y", SvgNamespace, y.Value.ToString()!, "svg");
        }
        if (width.HasValue)
        {
            drawControl.SetAttribute("width", SvgNamespace, width.Value.ToString()!, "svg");
        }
        if (height.HasValue)
        {
            drawControl.SetAttribute("height", SvgNamespace, height.Value.ToString()!, "svg");
        }

        p.AppendChild(drawControl);
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string namespaceUri, string prefix)
    {
        var child = FindChildElement(parent, localName, namespaceUri);
        if (child is not null)
        {
            return child;
        }

        child = OdfNodeFactory.CreateElement(localName, namespaceUri, prefix);
        parent.AppendChild(child);
        return child;
    }

    private OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (var child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }
        return null;
    }
}
