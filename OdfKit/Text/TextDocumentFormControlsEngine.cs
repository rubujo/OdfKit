using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Forms;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Text;

/// <summary>
/// 文字文件表單控制項引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFormControlsEngine
{
    internal static OdfFormControl AddFormControl(
        TextDocumentMutationContext context,
        OdfControlType type,
        string name,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string label,
        IReadOnlyList<string>? listItems)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentFormControlsEngine_ControlCannotBeEmpty"), nameof(name));

        OdfNode formsNode = FindOrCreateFormsNode(context.BodyTextRoot);
        OdfNode formNode = TextDocumentDomHelper.FindOrCreateChild(formsNode, "form", OdfNamespaces.Form, "form");
        if (string.IsNullOrEmpty(formNode.GetAttribute("name", OdfNamespaces.Form)))
            formNode.SetAttribute("name", OdfNamespaces.Form, "Form1", "form");
        formNode.SetAttribute("apply-design-mode", OdfNamespaces.Form, "false", "form");

        string elemName = type switch
        {
            OdfControlType.CheckBox => "checkbox",
            OdfControlType.ListBox => "listbox",
            OdfControlType.Button => "button",
            _ => "text",
        };
        OdfNode ctrlNode = new(OdfNodeType.Element, elemName, OdfNamespaces.Form, "form");
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
                OdfNode optNode = new(OdfNodeType.Element, "option", OdfNamespaces.Form, "form");
                optNode.SetAttribute("label", OdfNamespaces.Form, item, "form");
                ctrlNode.AppendChild(optNode);
            }
        }

        formNode.AppendChild(ctrlNode);

        OdfNode para = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("name", OdfNamespaces.Draw, $"ctrl-{name}", "draw");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frame.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");

        OdfNode ctrlRef = new(OdfNodeType.Element, "control", OdfNamespaces.Draw, "draw");
        ctrlRef.SetAttribute("control", OdfNamespaces.Draw, name, "draw");
        frame.AppendChild(ctrlRef);
        para.AppendChild(frame);
        context.BodyTextRoot.AppendChild(para);

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

    internal static IReadOnlyList<OdfFormControl> GetFormControls(OdfNode bodyTextRoot)
    {
        var result = new List<OdfFormControl>();
        OdfNode? formsNode = FindFormsNode(bodyTextRoot);
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

    private static OdfNode FindOrCreateFormsNode(OdfNode bodyTextRoot)
    {
        OdfNode? existing = FindFormsNode(bodyTextRoot);
        if (existing is not null)
            return existing;

        OdfNode formsNode = new(OdfNodeType.Element, "forms", OdfNamespaces.Office, "office");
        if (bodyTextRoot.Children.Count > 0)
            bodyTextRoot.InsertBefore(formsNode, bodyTextRoot.Children[0]);
        else
            bodyTextRoot.AppendChild(formsNode);
        return formsNode;
    }

    private static OdfNode? FindFormsNode(OdfNode bodyTextRoot)
    {
        foreach (OdfNode child in bodyTextRoot.Children)
        {
            if (child.LocalName == "forms" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }

        return null;
    }
}
