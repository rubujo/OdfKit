using System;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 提供 typed DOM 元素的 Fluent 樣式混合設定器。
/// </summary>
public sealed class OdfStyleMixinBuilder
{
    private readonly OdfElement _element;
    private readonly OdfDocument _document;
    private readonly string _family;

    internal OdfStyleMixinBuilder(OdfElement element, string? family)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _document = element.Document
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfStyleMixinBuilder_DocumentRequired"));
        _family = string.IsNullOrWhiteSpace(family)
            ? InferStyleFamily(element)
            : family!;
    }

    /// <summary>
    /// 設定文字為粗體。
    /// </summary>
    /// <param name="enabled">是否啟用粗體</param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder Bold(bool enabled = true)
        => SetTextProperty("font-weight", OdfNamespaces.Fo, enabled ? "bold" : "normal", "fo");

    /// <summary>
    /// 設定文字為斜體。
    /// </summary>
    /// <param name="enabled">是否啟用斜體</param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder Italic(bool enabled = true)
        => SetTextProperty("font-style", OdfNamespaces.Fo, enabled ? "italic" : "normal", "fo");

    /// <summary>
    /// 設定文字底線。
    /// </summary>
    /// <param name="enabled">是否啟用底線</param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder Underline(bool enabled = true)
        => SetTextProperty("text-underline-style", OdfNamespaces.Style, enabled ? "solid" : "none", "style");

    /// <summary>
    /// 設定文字色彩。
    /// </summary>
    /// <param name="color">ODF 色彩字串，例如 <c>#336699</c></param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder Color(string? color)
        => SetTextProperty("color", OdfNamespaces.Fo, color, "fo");

    /// <summary>
    /// 設定字級。
    /// </summary>
    /// <param name="fontSize">ODF 長度字串，例如 <c>14pt</c></param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder FontSize(string? fontSize)
        => SetTextProperty("font-size", OdfNamespaces.Fo, fontSize, "fo");

    /// <summary>
    /// 設定文字對齊。
    /// </summary>
    /// <param name="alignment">ODF 對齊字串，例如 <c>start</c>、<c>center</c> 或 <c>end</c></param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder TextAlign(string? alignment)
        => SetFamilyProperty("paragraph-properties", "text-align", OdfNamespaces.Fo, alignment, "fo");

    /// <summary>
    /// 設定背景色彩。
    /// </summary>
    /// <param name="color">ODF 色彩字串，例如 <c>#FFFFCC</c></param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder BackgroundColor(string? color)
        => SetFamilyProperty(GetBackgroundPropertyElement(), "background-color", OdfNamespaces.Fo, color, "fo");

    /// <summary>
    /// 設定目前自動樣式要繼承的父樣式名稱。
    /// </summary>
    /// <param name="parentStyleName">父樣式名稱；傳入 <see langword="null"/> 或空白字串時移除繼承關聯</param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder InheritFrom(string? parentStyleName)
        => SetStyleAttribute("parent-style-name", parentStyleName);

    /// <summary>
    /// 設定目前自動樣式的 ODF 樣式 class。
    /// </summary>
    /// <param name="styleClass">樣式 class；傳入 <see langword="null"/> 或空白字串時移除 class</param>
    /// <returns>目前的樣式設定器，供鏈式呼叫使用</returns>
    public OdfStyleMixinBuilder StyleClass(string? styleClass)
        => SetStyleAttribute("class", styleClass);

    private OdfStyleMixinBuilder SetTextProperty(string localName, string namespaceUri, string? value, string prefix)
        => SetFamilyProperty("text-properties", localName, namespaceUri, value, prefix);

    private OdfStyleMixinBuilder SetStyleAttribute(string localName, string? value)
    {
        OdfNode styleNode = _document.StyleEngine.GetOrCreateLocalStyle(_element, _family);
        if (string.IsNullOrWhiteSpace(value))
        {
            styleNode.RemoveAttribute(localName, OdfNamespaces.Style);
        }
        else
        {
            styleNode.SetAttribute(localName, OdfNamespaces.Style, value!, "style");
        }

        _element.IsModified = true;
        return this;
    }

    private OdfStyleMixinBuilder SetFamilyProperty(
        string propertyElement,
        string localName,
        string namespaceUri,
        string? value,
        string prefix)
    {
        _document.StyleEngine.SetLocalStyleProperty(
            _element,
            _family,
            propertyElement,
            localName,
            namespaceUri,
            value,
            prefix,
            deferSave: true);
        return this;
    }

    private string GetBackgroundPropertyElement()
        => _family == "table-cell" ? "table-cell-properties" : "paragraph-properties";

    internal static string InferStyleFamily(OdfElement element)
    {
        if (element.NamespaceUri == OdfNamespaces.Table)
        {
            return element.LocalName switch
            {
                "table-cell" or "covered-table-cell" => "table-cell",
                "table-row" => "table-row",
                "table-column" => "table-column",
                _ => "table"
            };
        }

        if (element.NamespaceUri == OdfNamespaces.Draw)
        {
            return element.LocalName == "page" ? "drawing-page" : "graphic";
        }

        return element.LocalName is "p" or "h" ? "paragraph" : "text";
    }
}

/// <summary>
/// 提供 typed DOM 元素的 Fluent 樣式混合擴充方法。
/// </summary>
public static class OdfStyleMixinExtensions
{
    /// <summary>
    /// 對指定 typed DOM 元素套用 Fluent 樣式混合設定。
    /// </summary>
    /// <typeparam name="TElement">要套用樣式的 typed DOM 元素型別</typeparam>
    /// <param name="element">目標元素</param>
    /// <param name="configure">樣式設定委派</param>
    /// <param name="family">選用的 ODF 樣式家族；未指定時依元素推斷</param>
    /// <returns>原始元素，供鏈式呼叫使用</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="element"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出</exception>
    public static TElement ApplyStyle<TElement>(
        this TElement element,
        Action<OdfStyleMixinBuilder> configure,
        string? family = null)
        where TElement : OdfElement
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var builder = new OdfStyleMixinBuilder(element, family);
        configure(builder);
        element.Document!.StyleEngine.DeduplicateAndSaveStyles();
        element.InvalidateStyle();
        return element;
    }

    /// <summary>
    /// 對指定 typed DOM 元素設定自動樣式。
    /// </summary>
    /// <typeparam name="TElement">要設定樣式的 typed DOM 元素型別</typeparam>
    /// <param name="element">目標元素</param>
    /// <param name="configure">樣式設定委派</param>
    /// <param name="family">選用的 ODF 樣式家族；未指定時依元素推斷</param>
    /// <returns>原始元素，供鏈式呼叫使用</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="element"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出</exception>
    /// <remarks>
    /// 此方法是 <see cref="ApplyStyle{TElement}(TElement, Action{OdfStyleMixinBuilder}, string?)"/>
    /// 的計畫名入口，讓呼叫端可使用 <c>element.ConfigureStyle(...)</c> 設定享元自動樣式。
    /// </remarks>
    public static TElement ConfigureStyle<TElement>(
        this TElement element,
        Action<OdfStyleMixinBuilder> configure,
        string? family = null)
        where TElement : OdfElement
    {
        return element.ApplyStyle(configure, family);
    }

    /// <summary>
    /// 從來源元素複製樣式參照到指定 typed DOM 元素。
    /// </summary>
    /// <typeparam name="TElement">要套用樣式的 typed DOM 元素型別</typeparam>
    /// <param name="element">目標元素</param>
    /// <param name="source">來源元素</param>
    /// <param name="family">選用的 ODF 樣式家族；未指定時依目標元素推斷</param>
    /// <returns>原始目標元素，供鏈式呼叫使用</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="element"/> 或 <paramref name="source"/> 為 <see langword="null"/> 時擲出</exception>
    /// <remarks>
    /// 此方法會先讓目標元素共用來源元素的 <c>style-name</c>。若之後再對目標元素呼叫
    /// <see cref="ConfigureStyle{TElement}(TElement, Action{OdfStyleMixinBuilder}, string?)"/>，
    /// 現有局部樣式管線會以該樣式作為父樣式建立新的自動樣式，避免修改來源元素。
    /// </remarks>
    public static TElement CopyFormatFrom<TElement>(
        this TElement element,
        OdfElement source,
        string? family = null)
        where TElement : OdfElement
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        string resolvedFamily = string.IsNullOrWhiteSpace(family)
            ? OdfStyleMixinBuilder.InferStyleFamily(element)
            : family!;
        string styleNamespace = GetStyleAttributeNamespace(resolvedFamily);
        string? sourceStyleName = source.GetAttribute("style-name", styleNamespace);

        element.Document?.StyleEngine.ReleaseLocalStyle(element);
        if (string.IsNullOrWhiteSpace(sourceStyleName))
        {
            element.RemoveAttribute("style-name", styleNamespace);
        }
        else
        {
            element.SetAttribute(
                "style-name",
                styleNamespace,
                sourceStyleName!,
                OdfNamespaces.GetPrefix(styleNamespace));
        }

        element.IsModified = true;
        element.InvalidateStyle();
        return element;
    }

    private static string GetStyleAttributeNamespace(string family)
        => family switch
        {
            "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
            "graphic" or "drawing-page" => OdfNamespaces.Draw,
            _ => OdfNamespaces.Text
        };
}
