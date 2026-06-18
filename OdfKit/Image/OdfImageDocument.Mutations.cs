using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

public partial class OdfImageDocument
{
    /// <summary>
    /// 依名稱尋找影像框架摘要。
    /// </summary>
    /// <param name="name">框架名稱（<c>draw:name</c>）。</param>
    /// <returns>符合名稱的框架摘要；找不到時為 <see langword="null"/>。</returns>
    public OdfImageFrameInfo? TryGetImageFrame(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("框架名稱不能為空。", nameof(name));
        }

        foreach (OdfImageFrameInfo frame in GetImageFrames())
        {
            if (string.Equals(frame.Name, name, StringComparison.Ordinal))
            {
                return frame;
            }
        }

        return null;
    }

    /// <summary>
    /// 更新指定名稱影像框架的版面與中繼資料。
    /// </summary>
    /// <param name="name">框架名稱。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">框架寬度。</param>
    /// <param name="height">框架高度。</param>
    /// <param name="title">選用的框架標題。</param>
    /// <param name="description">選用的框架描述。</param>
    /// <returns>若成功更新則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool UpdateImageFrame(
        string name,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string? title = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("框架名稱不能為空。", nameof(name));
        }

        OdfNode? frame = FindFrameByName(name);
        if (frame is null)
        {
            return false;
        }

        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        SetOptionalChildText(frame, "title", OdfNamespaces.Svg, "svg", title);
        SetOptionalChildText(frame, "desc", OdfNamespaces.Svg, "svg", description);
        return true;
    }

    /// <summary>
    /// 移除指定名稱的影像框架。
    /// </summary>
    /// <param name="name">框架名稱。</param>
    /// <returns>若成功移除則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool RemoveImageFrame(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("框架名稱不能為空。", nameof(name));
        }

        OdfNode? frame = FindFrameByName(name);
        if (frame?.Parent is null)
        {
            return false;
        }

        frame.Parent.RemoveChild(frame);
        return true;
    }

    private OdfNode? FindFrameByName(string name)
    {
        foreach (OdfNode child in GetImageNode().Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "frame" &&
                child.NamespaceUri == OdfNamespaces.Draw &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Draw), name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }
}
