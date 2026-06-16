using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region 表單控制項（Form Controls）


    /// <summary>
    /// 在文件中加入表單控制項（draw:frame + office:forms 定義）。
    /// </summary>
    /// <param name="type">控制項類型。</param>
    /// <param name="name">控制項名稱（唯一識別字）。</param>
    /// <param name="x">控制項左邊距。</param>
    /// <param name="y">控制項上邊距。</param>
    /// <param name="width">控制項寬度。</param>
    /// <param name="height">控制項高度。</param>
    /// <param name="label">控制項標籤文字（核取方塊、按鈕）或預設值（文字欄位）。</param>
    /// <param name="listItems">下拉式清單選項（僅 ListBox 有效）。</param>
    /// <returns>描述新控制項的 <see cref="OdfFormControl"/> 物件。</returns>
    public OdfFormControl AddFormControl(
        OdfControlType type,
        string name,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string label = "",
        IReadOnlyList<string>? listItems = null)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("控制項名稱不可為空。", nameof(name));

        // 1. 建立/取得 <office:forms><form:form>
        OdfNode formsNode = FindOrCreateFormsNode();
        OdfNode formNode = FindOrCreateChild(formsNode, "form", OdfNamespaces.Form, "form");
        if (string.IsNullOrEmpty(formNode.GetAttribute("name", OdfNamespaces.Form)))
            formNode.SetAttribute("name", OdfNamespaces.Form, "Form1", "form");
        formNode.SetAttribute("apply-design-mode", OdfNamespaces.Form, "false", "form");

        // 2. 建立控制項 form:* 元素
        string elemName = type switch
        {
            OdfControlType.CheckBox => "checkbox",
            OdfControlType.ListBox => "listbox",
            OdfControlType.Button => "button",
            _ => "text",
        };
        OdfNode ctrlNode = new OdfNode(OdfNodeType.Element, elemName, OdfNamespaces.Form, "form");
        ctrlNode.SetAttribute("name", OdfNamespaces.Form, name, "form");
        ctrlNode.SetAttribute("id", OdfNamespaces.Form, name, "form");
        if (!string.IsNullOrEmpty(label))
            ctrlNode.SetAttribute("label", OdfNamespaces.Form, label, "form");
        if (type == OdfControlType.TextBox && !string.IsNullOrEmpty(label))
            ctrlNode.SetAttribute("value", OdfNamespaces.Form, label, "form");
        if (type == OdfControlType.CheckBox)
            ctrlNode.SetAttribute("current-state", OdfNamespaces.Form, "unchecked", "form");

        if (type == OdfControlType.ListBox && listItems is not null)
        {
            foreach (string item in listItems)
            {
                OdfNode optNode = new OdfNode(OdfNodeType.Element, "option", OdfNamespaces.Form, "form");
                optNode.SetAttribute("label", OdfNamespaces.Form, item, "form");
                ctrlNode.AppendChild(optNode);
            }
        }
        formNode.AppendChild(ctrlNode);

        // 3. 建立 draw:frame 錨點段落
        OdfNode para = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        OdfNode frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("name", OdfNamespaces.Draw, $"ctrl-{name}", "draw");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frame.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");

        OdfNode ctrlRef = new OdfNode(OdfNodeType.Element, "control", OdfNamespaces.Draw, "draw");
        ctrlRef.SetAttribute("control", OdfNamespaces.Draw, name, "draw");
        frame.AppendChild(ctrlRef);
        para.AppendChild(frame);
        BodyTextRoot.AppendChild(para);

        return new OdfFormControl
        {
            ControlType = type,
            Name = name,
            Label = label,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ListItems = listItems ?? [],
        };
    }

    /// <summary>
    /// 取得文件中所有表單控制項。
    /// </summary>
    /// <returns>控制項清單；若無表單則回傳空清單。</returns>
    public IReadOnlyList<OdfFormControl> GetFormControls()
    {
        var result = new List<OdfFormControl>();
        OdfNode? formsNode = FindFormsNode();
        if (formsNode is null)
            return result;

        foreach (OdfNode formNode in formsNode.Children)
        {
            if (formNode.LocalName != "form" || formNode.NamespaceUri != OdfNamespaces.Form)
                continue;

            foreach (OdfNode ctrl in formNode.Children)
            {
                if (ctrl.NamespaceUri != OdfNamespaces.Form)
                    continue;

                OdfControlType type = ctrl.LocalName switch
                {
                    "checkbox" => OdfControlType.CheckBox,
                    "listbox" => OdfControlType.ListBox,
                    "button" => OdfControlType.Button,
                    _ => OdfControlType.TextBox,
                };

                var items = new List<string>();
                foreach (OdfNode child in ctrl.Children)
                {
                    if (child.LocalName == "option" && child.NamespaceUri == OdfNamespaces.Form)
                    {
                        string? optLabel = child.GetAttribute("label", OdfNamespaces.Form);
                        if (!string.IsNullOrEmpty(optLabel))
                            items.Add(optLabel!);
                    }
                }

                result.Add(new OdfFormControl
                {
                    ControlType = type,
                    Name = ctrl.GetAttribute("name", OdfNamespaces.Form) ?? string.Empty,
                    Label = ctrl.GetAttribute("label", OdfNamespaces.Form) ?? string.Empty,
                    Value = ctrl.GetAttribute("value", OdfNamespaces.Form),
                    IsChecked = ctrl.GetAttribute("current-state", OdfNamespaces.Form) == "checked",
                    ListItems = items,
                });
            }
        }

        return result;
    }

    private OdfNode FindOrCreateFormsNode()
    {
        OdfNode? existing = FindFormsNode();
        if (existing is not null)
            return existing;

        OdfNode formsNode = new OdfNode(OdfNodeType.Element, "forms", OdfNamespaces.Office, "office");
        if (BodyTextRoot.Children.Count > 0)
            BodyTextRoot.InsertBefore(formsNode, BodyTextRoot.Children[0]);
        else
            BodyTextRoot.AppendChild(formsNode);
        return formsNode;
    }

    private OdfNode? FindFormsNode()
    {
        foreach (OdfNode child in BodyTextRoot.Children)
        {
            if (child.LocalName == "forms" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }
        return null;
    }


    #endregion
}
