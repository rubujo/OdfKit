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
    private static readonly HashSet<string> SupportedControlNames = new(StringComparer.Ordinal)
    {
        "text",
        "checkbox",
        "listbox",
        "button",
    };

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
                if (ctrl.NamespaceUri != OdfNamespaces.Form || !SupportedControlNames.Contains(ctrl.LocalName))
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

                string name = ctrl.GetAttribute("name", OdfNamespaces.Form) ?? string.Empty;
                OdfNode? frame = FindControlFrame(bodyTextRoot, name);
                result.Add(new OdfFormControl
                {
                    ControlType = type,
                    Name = name,
                    Label = ctrl.GetAttribute("label", OdfNamespaces.Form) ?? string.Empty,
                    Value = ctrl.GetAttribute("value", OdfNamespaces.Form),
                    IsChecked = ctrl.GetAttribute("current-state", OdfNamespaces.Form) == "checked",
                    ListItems = items,
                    X = ParseLength(frame?.GetAttribute("x", OdfNamespaces.Svg)),
                    Y = ParseLength(frame?.GetAttribute("y", OdfNamespaces.Svg)),
                    Width = ParseLength(frame?.GetAttribute("width", OdfNamespaces.Svg)),
                    Height = ParseLength(frame?.GetAttribute("height", OdfNamespaces.Svg)),
                });
            }
        }

        return result;
    }

    internal static bool UpdateFormControl(
        OdfNode bodyTextRoot,
        string name,
        string label,
        string? value,
        bool isChecked,
        IReadOnlyList<string>? listItems)
    {
        OdfNode? control = FindControlNode(bodyTextRoot, name);
        if (control is null)
            return false;

        SetOptionalAttribute(control, "label", label);
        SetOptionalAttribute(control, "value", value);
        if (control.LocalName == "checkbox")
            control.SetAttribute("current-state", OdfNamespaces.Form, isChecked ? "checked" : "unchecked", "form");

        if (control.LocalName == "listbox" && listItems is not null)
        {
            foreach (OdfNode child in new List<OdfNode>(control.Children))
            {
                if (child.LocalName == "option" && child.NamespaceUri == OdfNamespaces.Form)
                    control.RemoveChild(child);
            }
            foreach (string item in listItems)
            {
                var option = new OdfNode(OdfNodeType.Element, "option", OdfNamespaces.Form, "form");
                option.SetAttribute("label", OdfNamespaces.Form, item, "form");
                control.AppendChild(option);
            }
        }
        return true;
    }

    internal static bool RemoveFormControl(OdfNode bodyTextRoot, string name)
    {
        OdfNode? control = FindControlNode(bodyTextRoot, name);
        if (control?.Parent is null)
            return false;

        OdfNode form = control.Parent;
        form.RemoveChild(control);
        RemoveControlFrames(bodyTextRoot, name);
        if (form.Children.Count == 0)
        {
            OdfNode? forms = form.Parent;
            forms?.RemoveChild(form);
            if (forms is not null && forms.Children.Count == 0)
                forms.Parent?.RemoveChild(forms);
        }
        return true;
    }

    internal static int ClearFormControls(OdfNode bodyTextRoot)
    {
        IReadOnlyList<OdfFormControl> controls = GetFormControls(bodyTextRoot);
        int removed = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (OdfFormControl control in controls)
        {
            if (names.Add(control.Name) && RemoveFormControl(bodyTextRoot, control.Name))
                removed++;
        }
        return removed;
    }

    private static OdfNode? FindControlNode(OdfNode bodyTextRoot, string name)
    {
        OdfNode? forms = FindFormsNode(bodyTextRoot);
        if (forms is null)
            return null;
        foreach (OdfNode form in forms.Children)
        {
            foreach (OdfNode control in form.Children)
            {
                if (control.NamespaceUri == OdfNamespaces.Form &&
                    SupportedControlNames.Contains(control.LocalName) &&
                    string.Equals(control.GetAttribute("name", OdfNamespaces.Form), name, StringComparison.Ordinal))
                {
                    return control;
                }
            }
        }
        return null;
    }

    private static OdfNode? FindControlFrame(OdfNode root, string name)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                foreach (OdfNode descendant in child.Descendants())
                {
                    if (descendant.LocalName == "control" &&
                        descendant.NamespaceUri == OdfNamespaces.Draw &&
                        string.Equals(descendant.GetAttribute("control", OdfNamespaces.Draw), name, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }
            OdfNode? nested = FindControlFrame(child, name);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static void RemoveControlFrames(OdfNode root, string name)
    {
        var removals = new List<OdfNode>();
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw && FrameReferencesControl(child, name))
                removals.Add(child);
            else
                RemoveControlFrames(child, name);
        }
        foreach (OdfNode frame in removals)
        {
            OdfNode? parent = frame.Parent;
            parent?.RemoveChild(frame);
            if (parent is not null && parent.LocalName == "p" && parent.NamespaceUri == OdfNamespaces.Text && parent.Children.Count == 0)
                parent.Parent?.RemoveChild(parent);
        }
    }

    private static bool FrameReferencesControl(OdfNode frame, string name)
    {
        foreach (OdfNode node in frame.Descendants())
        {
            if (node.LocalName == "control" && node.NamespaceUri == OdfNamespaces.Draw &&
                string.Equals(node.GetAttribute("control", OdfNamespaces.Draw), name, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void SetOptionalAttribute(OdfNode node, string localName, string? value)
    {
        if (value is null)
            node.RemoveAttribute(localName, OdfNamespaces.Form);
        else
            node.SetAttribute(localName, OdfNamespaces.Form, value, "form");
    }

    private static OdfLength ParseLength(string? value) =>
        OdfLength.TryParse(value, out OdfLength length) ? length : default;

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
