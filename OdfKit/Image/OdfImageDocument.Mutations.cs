using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Image;
/// <summary>
/// Provides the OdfImageDocument API.
/// 提供 OdfImageDocument API。
/// </summary>

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
    /// Batch-sets the rotation angle for image frames.
    /// 批次設定影像框架的旋轉角度。
    /// </summary>
    /// <param name="names">The frame names. / 框架名稱清單。</param>
    /// <param name="degrees">The rotation angle in degrees; <see langword="null"/> removes rotation. / 旋轉角度（度）；<see langword="null"/> 表示移除旋轉設定。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="names"/> is <see langword="null"/>. / 當 <paramref name="names"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfImageBatchUpdateResult SetImageRotations(IEnumerable<string> names, double? degrees)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        var result = new OdfImageBatchUpdateResult();
        foreach (string name in names)
        {
            if (SetImageRotation(name, degrees))
            {
                result.UpdatedCount++;
            }
            else
            {
                result.MissingNames.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// Batch-sets crop bounds for image frames.
    /// 批次設定影像框架的裁切邊界。
    /// </summary>
    /// <param name="names">The frame names. / 框架名稱清單。</param>
    /// <param name="crop">The crop bounds; <see langword="null"/> removes crop settings. / 裁切邊界；<see langword="null"/> 表示移除裁切設定。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="names"/> is <see langword="null"/>. / 當 <paramref name="names"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfImageBatchUpdateResult SetImageCrops(IEnumerable<string> names, OdfImageCropInfo? crop)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        var result = new OdfImageBatchUpdateResult();
        foreach (string name in names)
        {
            if (SetImageCrop(name, crop))
            {
                result.UpdatedCount++;
            }
            else
            {
                result.MissingNames.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// Finds an image frame summary by name.
    /// 依名稱尋找影像框架摘要。
    /// </summary>
    /// <param name="name">The frame name (<c>draw:name</c>). / 框架名稱（<c>draw:name</c>）。</param>
    /// <returns>The matching frame summary, or <see langword="null"/> if not found. / 符合名稱的框架摘要；找不到時為 <see langword="null"/>。</returns>
    public OdfImageFrameInfo? FindImageFrame(string name)
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
    /// Replaces the image content of the named frame while preserving its layout and metadata.
    /// 替換具名框架的影像內容，同時保留其版面與中繼資料。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="imageBytes">The replacement image bytes. / 替換用的影像位元組。</param>
    /// <returns><see langword="true"/> if the frame was updated; otherwise <see langword="false"/>. / 若成功更新框架則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool ReplaceImageFrameContent(string name, byte[] imageBytes) =>
        ReplaceImageFrameContent(name, imageBytes, null);

    /// <summary>
    /// Replaces the image content of the named frame while preserving its layout and metadata.
    /// 替換具名框架的影像內容，同時保留其版面與中繼資料。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="imageBytes">The replacement image bytes. / 替換用的影像位元組。</param>
    /// <param name="preferredName">The preferred package file name. / 偏好的封裝檔名。</param>
    /// <returns><see langword="true"/> if the frame was updated; otherwise <see langword="false"/>. / 若成功更新框架則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool ReplaceImageFrameContent(string name, byte[] imageBytes, string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty_3"), nameof(name));
        }

        if (imageBytes is null)
        {
            throw new ArgumentNullException(nameof(imageBytes));
        }

        OdfNode? frame = FindFrameByName(name);
        OdfNode? image = frame is null ? null : FindChild(frame, "image", OdfNamespaces.Draw);
        if (image is null)
        {
            return false;
        }

        string href = new OdfMediaManager(Package).AddImage(imageBytes, preferredName);
        image.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        return true;
    }

    /// <summary>
    /// Replaces image content for multiple named frames in request order.
    /// 依要求順序替換多個具名框架的影像內容。
    /// </summary>
    /// <remarks>
    /// Every request is validated before package resources are changed. Layout, metadata, and unrelated frame content are preserved.
    /// 所有要求都會在變更封裝資源前完成驗證；版面、中繼資料與無關框架內容都會保留。
    /// </remarks>
    /// <param name="updates">The image content replacement requests. / 影像內容替換要求。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="updates"/>, an update, or its image bytes are <see langword="null"/>. / 當 <paramref name="updates"/>、任一更新要求或其影像位元組為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When an update frame name is blank. / 當任一更新要求的框架名稱為空白時擲出。</exception>
    public OdfImageBatchUpdateResult ReplaceImageFrameContents(
        IEnumerable<OdfImageContentUpdate> updates)
    {
        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        var requests = new List<OdfImageContentUpdate>();
        foreach (OdfImageContentUpdate update in updates)
        {
            if (update is null || update.ImageBytes is null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (string.IsNullOrWhiteSpace(update.Name))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty_3"),
                    nameof(updates));
            }

            requests.Add(update);
        }

        var result = new OdfImageBatchUpdateResult();
        foreach (OdfImageContentUpdate update in requests)
        {
            if (ReplaceImageFrameContent(update.Name, update.ImageBytes, update.PreferredName))
            {
                result.UpdatedCount++;
            }
            else
            {
                result.MissingNames.Add(update.Name);
            }
        }

        return result;
    }

    /// <summary>
    /// Short overload of UpdateImageFrame that accepts name, x, y, width, and height; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、x、y、width 與 height；其餘可選參數使用預設值並轉呼叫最長 UpdateImageFrame 多載。
    /// </summary>
    public bool UpdateImageFrame(string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height) => UpdateImageFrame(name, x, y, width, height, null, null);

    /// <summary>
    /// Short overload of UpdateImageFrame that accepts name, x, y, width, height, and title; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、x、y、width、height 與 title；其餘可選參數使用預設值並轉呼叫最長 UpdateImageFrame 多載。
    /// </summary>
    public bool UpdateImageFrame(string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height, string? title) => UpdateImageFrame(name, x, y, width, height, title, null);


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
    public bool UpdateImageFrame(string name, OdfLength x, OdfLength y, OdfLength width, OdfLength height, string? title, string? description)
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
