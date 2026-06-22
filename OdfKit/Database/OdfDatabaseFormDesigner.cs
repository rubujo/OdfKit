using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 資料庫的表單設計器，用於建模表單屬性與常用控制項。
/// <para>
/// 報表內容（LibreOffice Base「使用嚮導建立報表」產生的型式）應改用 <see cref="OdfKit.Text.TextDocument"/>
/// 搭配 <see cref="OdfKit.Text.OdfParagraph.AddDatabaseDisplayField"/>／
/// <see cref="OdfKit.Text.OdfParagraph.AddDatabaseNextField"/> 建立為獨立文件，再透過
/// <see cref="OdfKit.Database.OdfDatabaseDocument.AddReport"/> 的 href 參照機制連結至 .odb 套件。
/// 不存在官方 OASIS ODF 報表結構 schema（先前版本的 <c>DefineReportStructure</c> 使用了虛構的
/// <c>urn:oasis:names:tc:opendocument:xmlns:report:1.0</c> 命名空間，已移除）。
/// </para>
/// </summary>
public sealed class OdfDatabaseFormDesigner
{
    private const string FormNamespace = "urn:oasis:names:tc:opendocument:xmlns:form:1.0";
    private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const string DrawNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private const string SvgNamespace = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

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
    /// 新增單選按鈕控制項（form:radio）。
    /// </summary>
    /// <param name="name">控制項名稱；同一群組內的單選按鈕應使用相同名稱。</param>
    /// <param name="label">顯示的標籤文字。</param>
    /// <param name="isSelected">預設選取狀態。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的單選按鈕 OdfNode 節點。</returns>
    public OdfNode AddRadioButton(
        string name,
        string label,
        bool isSelected = false,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var radioNode = OdfNodeFactory.CreateElement("radio", FormNamespace, "form");
        radioNode.SetAttribute("name", FormNamespace, name, "form");
        radioNode.SetAttribute("id", FormNamespace, id, "form");
        radioNode.SetAttribute("label", FormNamespace, label, "form");
        radioNode.SetAttribute("current-selected", FormNamespace, isSelected ? "true" : "false", "form");

        _formNode.AppendChild(radioNode);
        AddDrawControl(id, x, y, width, height);
        return radioNode;
    }

    /// <summary>
    /// 新增可編輯下拉組合方塊控制項（form:combobox）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="items">下拉選單的選項清單。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的組合方塊 OdfNode 節點。</returns>
    public OdfNode AddComboBox(
        string name,
        string label,
        IEnumerable<string>? items = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var comboNode = OdfNodeFactory.CreateElement("combobox", FormNamespace, "form");
        comboNode.SetAttribute("name", FormNamespace, name, "form");
        comboNode.SetAttribute("id", FormNamespace, id, "form");
        AddLabelProperty(comboNode, label);

        if (items is not null)
        {
            foreach (var item in items)
            {
                var option = OdfNodeFactory.CreateElement("item", FormNamespace, "form");
                option.TextContent = item;
                comboNode.AppendChild(option);
            }
        }

        _formNode.AppendChild(comboNode);
        AddDrawControl(id, x, y, width, height);
        return comboNode;
    }

    /// <summary>
    /// 新增數值輸入控制項（form:number）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="value">預設數值。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的數值輸入 OdfNode 節點。</returns>
    public OdfNode AddNumericField(
        string name,
        string label,
        double? value = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var numberNode = OdfNodeFactory.CreateElement("number", FormNamespace, "form");
        numberNode.SetAttribute("name", FormNamespace, name, "form");
        numberNode.SetAttribute("id", FormNamespace, id, "form");
        if (value.HasValue)
        {
            string text = value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            numberNode.SetAttribute("value", FormNamespace, text, "form");
            numberNode.SetAttribute("current-value", FormNamespace, text, "form");
        }

        AddLabelProperty(numberNode, label);
        _formNode.AppendChild(numberNode);
        AddDrawControl(id, x, y, width, height);
        return numberNode;
    }

    /// <summary>
    /// 新增日期輸入控制項（form:date）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="value">預設日期。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的日期輸入 OdfNode 節點。</returns>
    public OdfNode AddDateField(
        string name,
        string label,
        DateTime? value = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var dateNode = OdfNodeFactory.CreateElement("date", FormNamespace, "form");
        dateNode.SetAttribute("name", FormNamespace, name, "form");
        dateNode.SetAttribute("id", FormNamespace, id, "form");
        if (value.HasValue)
        {
            string text = value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            dateNode.SetAttribute("value", FormNamespace, text, "form");
            dateNode.SetAttribute("current-value", FormNamespace, text, "form");
        }

        AddLabelProperty(dateNode, label);
        _formNode.AppendChild(dateNode);
        AddDrawControl(id, x, y, width, height);
        return dateNode;
    }

    /// <summary>
    /// 新增時間輸入控制項（form:time）。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">標籤文字。</param>
    /// <param name="value">預設時間。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的時間輸入 OdfNode 節點。</returns>
    public OdfNode AddTimeField(
        string name,
        string label,
        TimeSpan? value = null,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var timeNode = OdfNodeFactory.CreateElement("time", FormNamespace, "form");
        timeNode.SetAttribute("name", FormNamespace, name, "form");
        timeNode.SetAttribute("id", FormNamespace, id, "form");
        if (value.HasValue)
        {
            string text = value.Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
            timeNode.SetAttribute("value", FormNamespace, text, "form");
            timeNode.SetAttribute("current-value", FormNamespace, text, "form");
        }

        AddLabelProperty(timeNode, label);
        _formNode.AppendChild(timeNode);
        AddDrawControl(id, x, y, width, height);
        return timeNode;
    }

    /// <summary>
    /// 為控制項繫結一個事件監聽器（<c>office:event-listeners</c>／<c>script:event-listener</c>）。
    /// </summary>
    /// <param name="controlNode">要繫結事件的控制項節點。</param>
    /// <param name="eventName">事件名稱（例如 <c>form:approveaction</c>）。</param>
    /// <param name="macroName">巨集名稱或位置。</param>
    /// <param name="language">巨集語言，預設為 <c>ooo:script</c>。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="controlNode"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">當 <paramref name="eventName"/> 或 <paramref name="macroName"/> 為空白時擲出。</exception>
    public void SetControlEvent(OdfNode controlNode, string eventName, string macroName, string language = "ooo:script")
    {
        if (controlNode is null)
        {
            throw new ArgumentNullException(nameof(controlNode));
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("事件名稱不能為空。", nameof(eventName));
        }

        if (string.IsNullOrWhiteSpace(macroName))
        {
            throw new ArgumentException("巨集名稱不能為空。", nameof(macroName));
        }

        const string OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        const string ScriptNs = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";

        OdfNode listeners = FindOrCreateChild(controlNode, "event-listeners", OfficeNs, "office");
        OdfNode? listener = null;
        foreach (OdfNode child in listeners.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "event-listener" &&
                child.NamespaceUri == ScriptNs &&
                string.Equals(child.GetAttribute("event-name", ScriptNs), eventName, StringComparison.Ordinal))
            {
                listener = child;
                break;
            }
        }

        if (listener is null)
        {
            listener = OdfNodeFactory.CreateElement("event-listener", ScriptNs, "script");
            listener.SetAttribute("event-name", ScriptNs, eventName, "script");
            listeners.AppendChild(listener);
        }

        listener.SetAttribute("language", ScriptNs, language, "script");
        listener.SetAttribute("macro-name", ScriptNs, macroName, "script");
    }

    /// <summary>
    /// 設定控制項是否為必填（對應 <c>form:input-required</c>）。
    /// </summary>
    /// <param name="controlNode">控制項節點。</param>
    /// <param name="required">是否必填。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="controlNode"/> 為 <see langword="null"/> 時擲出。</exception>
    public void SetControlRequired(OdfNode controlNode, bool required)
    {
        if (controlNode is null)
        {
            throw new ArgumentNullException(nameof(controlNode));
        }

        controlNode.SetAttribute("input-required", FormNamespace, required ? "true" : "false", "form");
    }

    /// <summary>
    /// 設定控制項允許輸入的最大字元長度（對應 <c>form:max-length</c>）。
    /// </summary>
    /// <param name="controlNode">控制項節點。</param>
    /// <param name="maxLength">最大字元長度；<c>0</c> 表示不限制。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="controlNode"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="maxLength"/> 為負數時擲出。</exception>
    public void SetControlMaxLength(OdfNode controlNode, int maxLength)
    {
        if (controlNode is null)
        {
            throw new ArgumentNullException(nameof(controlNode));
        }

        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "最大字元長度不能為負數。");
        }

        controlNode.SetAttribute("max-length", FormNamespace, maxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), "form");
    }

    /// <summary>
    /// 新增群組框控制項（form:frame），用於以標題群組相關控制項。
    /// </summary>
    /// <param name="name">控制項名稱。</param>
    /// <param name="label">群組框標題文字。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <returns>代表新增的群組框 OdfNode 節點。</returns>
    public OdfNode AddGroupBox(
        string name,
        string label,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null)
    {
        var id = $"control{_controlCounter++}";
        var frameNode = OdfNodeFactory.CreateElement("frame", FormNamespace, "form");
        frameNode.SetAttribute("name", FormNamespace, name, "form");
        frameNode.SetAttribute("id", FormNamespace, id, "form");
        if (!string.IsNullOrWhiteSpace(label))
        {
            frameNode.SetAttribute("label", FormNamespace, label, "form");
        }

        _formNode.AppendChild(frameNode);
        AddDrawControl(id, x, y, width, height);
        return frameNode;
    }

    private void AddLabelProperty(OdfNode controlNode, string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        var props = OdfNodeFactory.CreateElement("properties", FormNamespace, "form");
        controlNode.AppendChild(props);

        var prop = OdfNodeFactory.CreateElement("property", FormNamespace, "form");
        prop.SetAttribute("property-name", FormNamespace, "Label", "form");
        prop.SetAttribute("value-type", OfficeNamespace, "string", "office");
        prop.SetAttribute("string-value", FormNamespace, label!, "form");
        props.AppendChild(prop);
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
