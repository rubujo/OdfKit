using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Image;

public partial class OdfImageDocument
{
    /// <summary>
    /// Sets the rotation angle of the image frame with the specified name.
    /// 設定指定名稱影像框架的旋轉角度。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="degrees">The rotation angle in degrees; <see langword="null"/> removes the rotation setting. / 旋轉角度（度）；<see langword="null"/> 表示移除旋轉設定。</param>
    /// <returns><see langword="true"/> if set successfully; <see langword="false"/> if the frame is not found. / 若成功設定則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool SetImageRotation(string name, double? degrees)
    {
        OdfNode? frame = FindFrameByName(name);
        if (frame is null)
        {
            return false;
        }

        if (degrees is null)
        {
            frame.RemoveAttribute("transform", OdfNamespaces.Draw);
            return true;
        }

        double radians = degrees.Value * System.Math.PI / 180.0;
        frame.SetAttribute("transform", OdfNamespaces.Draw, $"rotate({radians.ToString(CultureInfo.InvariantCulture)})", "draw");
        return true;
    }

    /// <summary>
    /// Sets the crop bounds of the image frame with the specified name.
    /// 設定指定名稱影像框架的裁切邊界。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="crop">The crop bounds; <see langword="null"/> removes the existing crop setting. / 裁切邊界；<see langword="null"/> 表示移除既有裁切設定。</param>
    /// <returns><see langword="true"/> if set successfully; <see langword="false"/> if the frame is not found. / 若成功設定則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool SetImageCrop(string name, OdfImageCropInfo? crop)
    {
        OdfNode? frame = FindFrameByName(name);
        OdfNode? image = frame is null ? null : FindChild(frame, "image", OdfNamespaces.Draw);
        if (image is null)
        {
            return false;
        }

        if (crop is null)
        {
            image.RemoveAttribute("clip", OdfNamespaces.Fo);
            return true;
        }

        image.SetAttribute("clip", OdfNamespaces.Fo, crop.ToString(), "fo");
        return true;
    }

    /// <summary>
    /// Finds an image frame summary by name.
    /// 依名稱尋找影像框架摘要。
    /// </summary>
    /// <param name="name">The frame name (<c>draw:name</c>). / 框架名稱（<c>draw:name</c>）。</param>
    /// <returns>The matching frame summary, or <see langword="null"/> if not found. / 符合名稱的框架摘要；找不到時為 <see langword="null"/>。</returns>
    public OdfImageFrameInfo? TryGetImageFrame(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty_3"), nameof(name));
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
    /// Updates the layout and metadata of the image frame with the specified name.
    /// 更新指定名稱影像框架的版面與中繼資料。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The frame width. / 框架寬度。</param>
    /// <param name="height">The frame height. / 框架高度。</param>
    /// <param name="title">The optional frame title. / 選用的框架標題。</param>
    /// <param name="description">The optional frame description. / 選用的框架描述。</param>
    /// <returns><see langword="true"/> if updated successfully; <see langword="false"/> if the frame is not found. / 若成功更新則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
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
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty_3"), nameof(name));
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
    /// Removes the image frame with the specified name.
    /// 移除指定名稱的影像框架。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <returns><see langword="true"/> if removed successfully; <see langword="false"/> if the frame is not found. / 若成功移除則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool RemoveImageFrame(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty_3"), nameof(name));
        }

        OdfNode? frame = FindFrameByName(name);
        if (frame?.Parent is null)
        {
            return false;
        }

        frame.Parent.RemoveChild(frame);
        return true;
    }

    /// <summary>
    /// Batch-removes the image frames for the specified list of names.
    /// 批次移除指定名稱清單的影像框架。
    /// </summary>
    /// <param name="names">The list of frame names to remove. / 要移除的框架名稱清單。</param>
    /// <returns>The number of frames actually removed (names not found are ignored without throwing). / 實際成功移除的框架數量（找不到的名稱會被忽略，不會擲出例外）。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="names"/> is <see langword="null"/>. / 當 <paramref name="names"/> 為 <see langword="null"/> 時擲出。</exception>
    public int RemoveImageFrames(IEnumerable<string> names)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        int removedCount = 0;
        foreach (string name in names)
        {
            if (RemoveImageFrame(name))
            {
                removedCount++;
            }
        }

        return removedCount;
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
