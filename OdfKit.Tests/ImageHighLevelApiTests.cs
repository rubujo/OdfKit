using System.IO;
using OdfKit.Core;
using OdfKit.Image;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定影像文件高階 API 的整合測試。
/// </summary>
public class ImageHighLevelApiTests
{
    /// <summary>
    /// 驗證 <see cref="OdfImageDocument.GetImageFrames"/> 可讀回主要影像框架。
    /// </summary>
    [Fact]
    public void GetImageFrames_RoundTripsAfterSetImage()
    {
        using var image = OdfImageDocument.Create();
        byte[] bytes = CreatePngBytes();
        image.SetImageLayout(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(4),
            "PrimaryFrame",
            "主要照片",
            "主要影像描述。");
        string href = image.SetImage(bytes, "Primary.png");

        OdfImageFrameInfo frame = Assert.Single(image.GetImageFrames());
        Assert.Equal("PrimaryFrame", frame.Name);
        Assert.Equal("主要照片", frame.Title);
        Assert.Equal("主要影像描述。", frame.Description);
        Assert.Equal(href, frame.ImageHref);
        Assert.Equal("image/png", frame.MediaType);
        Assert.Equal(bytes.Length, frame.Size);
        Assert.True(frame.TryGetWidth(out OdfLength width));
        Assert.Equal(OdfLength.FromCentimeters(6), width);
    }

    /// <summary>
    /// 驗證 <see cref="OdfImageDocument.AddImageFrame"/> 與 <see cref="OdfImageDocument.GetImageFrames"/> 可列舉多個框架。
    /// </summary>
    [Fact]
    public void GetImageFrames_ReturnsMultipleFrames()
    {
        using var image = OdfImageDocument.Create();
        byte[] primary = CreatePngBytes();
        byte[] secondary = CreateAlternatePngBytes();

        image.SetImage(primary, "Primary.png");
        string secondaryHref = image.AddImageFrame(
            secondary,
            OdfLength.FromCentimeters(7),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(3),
            "Secondary.png",
            "SecondaryFrame",
            "附圖");

        Assert.Equal(2, image.GetImageFrames().Count);
        OdfImageFrameInfo secondaryFrame = image.GetImageFrames()[1];
        Assert.Equal("SecondaryFrame", secondaryFrame.Name);
        Assert.Equal("附圖", secondaryFrame.Title);
        Assert.Equal(secondaryHref, secondaryFrame.ImageHref);
        Assert.Equal(secondary.Length, secondaryFrame.Size);

        using var stream = new MemoryStream();
        image.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfImageDocument.Load(stream, "gallery.odi");
        Assert.Equal(2, loaded.GetImageFrames().Count);
        Assert.Equal("SecondaryFrame", loaded.GetImageFrames()[1].Name);
    }

    /// <summary>
    /// 驗證 <see cref="OdfImageDocument.UpdateImageFrame"/> 與 <see cref="OdfImageDocument.RemoveImageFrame"/> 可編輯多框架文件。
    /// </summary>
    [Fact]
    public void UpdateAndRemoveImageFrame_EditsMultiFrameDocument()
    {
        using var image = OdfImageDocument.Create();
        image.SetImage(CreatePngBytes(), "Primary.png");
        image.AddImageFrame(
            CreateAlternatePngBytes(),
            OdfLength.FromCentimeters(7),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(3),
            "Secondary.png",
            "SecondaryFrame",
            "附圖");

        Assert.True(image.UpdateImageFrame(
            "SecondaryFrame",
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(4),
            "更新附圖",
            "已調整版面。"));

        OdfImageFrameInfo? updated = image.TryGetImageFrame("SecondaryFrame");
        Assert.NotNull(updated);
        Assert.Equal("更新附圖", updated!.Title);
        Assert.Equal("已調整版面。", updated.Description);
        Assert.True(updated.TryGetX(out OdfLength x));
        Assert.Equal(OdfLength.FromCentimeters(8), x);
        Assert.True(updated.TryGetWidth(out OdfLength width));
        Assert.Equal(OdfLength.FromCentimeters(4), width);

        Assert.True(image.RemoveImageFrame("SecondaryFrame"));
        Assert.Single(image.GetImageFrames());
        Assert.Null(image.TryGetImageFrame("SecondaryFrame"));
    }

    /// <summary>
    /// 驗證框架更新與移除可於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void UpdateAndRemoveImageFrame_PersistAfterSaveAndLoad()
    {
        using var image = OdfImageDocument.Create();
        image.SetImage(CreatePngBytes(), "Primary.png");
        image.AddImageFrame(
            CreateAlternatePngBytes(),
            OdfLength.FromCentimeters(7),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(3),
            "Secondary.png",
            "SecondaryFrame",
            "附圖");

        using var stream = new MemoryStream();
        image.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = OdfImageDocument.Load(stream, "gallery.odi");
        Assert.True(loaded.UpdateImageFrame(
            "SecondaryFrame",
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(4),
            "更新附圖"));

        using var updatedStream = new MemoryStream();
        loaded.SaveToStream(updatedStream);
        updatedStream.Position = 0;

        using var reloaded = OdfImageDocument.Load(updatedStream, "gallery.odi");
        OdfImageFrameInfo? frame = reloaded.TryGetImageFrame("SecondaryFrame");
        Assert.NotNull(frame);
        Assert.Equal("更新附圖", frame!.Title);
        Assert.True(frame.TryGetX(out OdfLength x));
        Assert.Equal(OdfLength.FromCentimeters(8), x);

        Assert.True(reloaded.RemoveImageFrame("SecondaryFrame"));
        Assert.Single(reloaded.GetImageFrames());
    }

    private static byte[] CreatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static byte[] CreateAlternatePngBytes() =>
        System.Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAEklEQVR42mNkYGD4z0ABYBw1KgBvXQV0AAAAAElFTkSuQmCC");
}
