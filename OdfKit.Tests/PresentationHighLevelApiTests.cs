using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定簡報文件高階 API 的整合測試。
/// </summary>
public class PresentationHighLevelApiTests
{
    /// <summary>
    /// 驗證簡報動畫時間軸（進場、退場與強調動畫）API 之建立與 XML 結構。
    /// </summary>
    [Fact]
    public void AnimationTimelineApiWorksCorrectly()
    {
        using var document = PresentationDocument.Create();
        var slide = document.AddSlide();
        Assert.NotNull(slide);

        // 建立預留位置圖形以作為動畫目標
        var placeholder = slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("10.0cm"),
            OdfLength.Parse("2.0cm"));
        var shapeId = placeholder.Id;
        Assert.False(string.IsNullOrEmpty(shapeId));

        // 1. 測試進場動畫
        var entranceAnim = slide.AddEntranceEffect(shapeId, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick, TimeSpan.FromSeconds(0.5));
        Assert.NotNull(entranceAnim);
        Assert.Equal(shapeId, entranceAnim.TargetElementId);
        Assert.Equal(OdfAnimationEffect.Fade, entranceAnim.Effect);
        Assert.Equal(OdfAnimationTrigger.OnClick, entranceAnim.Trigger);

        // 驗證進場動畫 XML 節點結構
        var entranceNode = entranceAnim.Node;
        Assert.Equal("par", entranceNode.LocalName);
        Assert.Equal("0.50s", entranceNode.GetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("0.5s", entranceNode.GetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("entrance", entranceNode.GetAttribute("preset-class", OdfNamespaces.Presentation));
        Assert.Equal("ooo-entrance-fade-in", entranceNode.GetAttribute("preset-id", OdfNamespaces.Presentation));

        // 驗證包含 visibility=visible 的 set 節點
        var setVisibleNode = entranceNode.Children.FirstOrDefault(c => c.LocalName == "set");
        Assert.NotNull(setVisibleNode);
        Assert.Equal("visibility", setVisibleNode.GetAttribute("attributeName", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("visible", setVisibleNode.GetAttribute("to", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal(shapeId, setVisibleNode.GetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));

        // 驗證具體效果 transitionFilter 節點
        var filterNode = entranceNode.Children.FirstOrDefault(c => c.LocalName == "transitionFilter");
        Assert.NotNull(filterNode);
        Assert.Equal("fade", filterNode.GetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("in", filterNode.GetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));

        // 2. 測試退場動畫
        var exitAnim = slide.AddExitEffect(shapeId, OdfAnimationEffect.Zoom, OdfAnimationTrigger.WithPrevious, TimeSpan.FromSeconds(1.0));
        Assert.NotNull(exitAnim);
        Assert.Equal(shapeId, exitAnim.TargetElementId);
        Assert.Equal(OdfAnimationEffect.Zoom, exitAnim.Effect);
        Assert.Equal(OdfAnimationTrigger.WithPrevious, exitAnim.Trigger);

        var exitNode = exitAnim.Node;
        Assert.Equal("par", exitNode.LocalName);
        Assert.Equal("1.00s", exitNode.GetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("exit", exitNode.GetAttribute("preset-class", OdfNamespaces.Presentation));
        Assert.Equal("ooo-exit-zoom-out", exitNode.GetAttribute("preset-id", OdfNamespaces.Presentation));

        // 驗證包含 visibility=hidden 的 set 節點
        var setHiddenNode = exitNode.Children.FirstOrDefault(c => c.LocalName == "set");
        Assert.NotNull(setHiddenNode);
        Assert.Equal("visibility", setHiddenNode.GetAttribute("attributeName", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("hidden", setHiddenNode.GetAttribute("to", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));

        // 3. 測試強調動畫
        var emphasisAnim = slide.AddEmphasisEffect(shapeId, OdfAnimationEffect.Appear);
        Assert.NotNull(emphasisAnim);
        Assert.Equal(shapeId, emphasisAnim.TargetElementId);
        Assert.Equal(OdfAnimationEffect.Appear, emphasisAnim.Effect);

        var emphasisNode = emphasisAnim.Node;
        Assert.Equal("par", emphasisNode.LocalName);
        Assert.Equal("emphasis", emphasisNode.GetAttribute("preset-class", OdfNamespaces.Presentation));
    }

    /// <summary>
    /// 驗證投影片切換效果設定與 XML 結構。
    /// </summary>
    [Fact]
    public void SlideTransitionApiWorksCorrectly()
    {
        using var document = PresentationDocument.Create();
        document.AddSlide();

        // 1. 設定切換效果為 Push
        document.SetSlideTransition(0, OdfSlideTransition.Push);
        var slide = document.Slides[0];
        Assert.Equal("push", slide.Node.GetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("fromBottom", slide.Node.GetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Equal("automatic", slide.Node.GetAttribute("transition-type", OdfNamespaces.Presentation));

        // 2. 移除切換效果
        document.SetSlideTransition(0, OdfSlideTransition.None);
        Assert.Null(slide.Node.GetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
        Assert.Null(slide.Node.GetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"));
    }

    /// <summary>
    /// 驗證投影片母片新增與套用 API。
    /// </summary>
    [Fact]
    public void MasterPageApiWorksCorrectly()
    {
        using var document = PresentationDocument.Create();
        document.AddSlide();

        var def = new OdfMasterPageDefinition
        {
            Name = "CustomMaster",
            BackgroundColor = "#ff0000"
        };

        // 1. 新增母片
        var masterPage = document.AddMasterPage("CustomMaster", def);
        Assert.NotNull(masterPage);
        Assert.Equal("CustomMaster", masterPage.Name);

        // 驗證母片樣式 XML 結構
        var masterStyles = document.StylesRoot.Children.FirstOrDefault(c => c.LocalName == "master-styles" && c.NamespaceUri == OdfNamespaces.Office);
        Assert.NotNull(masterStyles);

        var pageNode = masterStyles.Children.FirstOrDefault(c => c.LocalName == "master-page" && c.GetAttribute("name", OdfNamespaces.Style) == "CustomMaster");
        Assert.NotNull(pageNode);

        var propsNode = pageNode.Children.FirstOrDefault(c => c.LocalName == "drawing-page-properties");
        Assert.NotNull(propsNode);
        Assert.Equal("solid", propsNode.GetAttribute("fill", OdfNamespaces.Draw));
        Assert.Equal("#ff0000", propsNode.GetAttribute("fill-color", OdfNamespaces.Draw));

        // 2. 套用母片至投影片
        document.SetMasterPage(0, "CustomMaster");
        Assert.Equal("CustomMaster", document.Slides[0].MasterPageName);
    }

    /// <summary>
    /// 驗證簡報投影片版面配置套用與預留位置自動生成 API。
    /// </summary>
    [Fact]
    public void PresentationLayoutApiWorksCorrectly()
    {
        using var document = PresentationDocument.Create();
        document.AddSlide();

        // 1. 測試預設版面（或空白）
        Assert.Equal(OdfPresentationLayout.Blank, document.GetLayout(0));

        // 2. 設定版面為 TitleOnly，驗證自動生成一個 Title 預留位置
        document.SetLayout(0, OdfPresentationLayout.TitleOnly);
        Assert.Equal(OdfPresentationLayout.TitleOnly, document.GetLayout(0));

        var slide = document.Slides[0];
        Assert.Single(slide.Placeholders);
        Assert.Equal(OdfPlaceholderType.Title, slide.Placeholders[0].PlaceholderType);

        // 3. 設定版面為 TitleAndBody，驗證舊預留位置清除並生成 Title 與 Outline 預留位置
        document.SetLayout(0, OdfPresentationLayout.TitleAndBody);
        Assert.Equal(OdfPresentationLayout.TitleAndBody, document.GetLayout(0));

        Assert.Equal(2, slide.Placeholders.Count);
        Assert.Contains(slide.Placeholders, p => p.PlaceholderType == OdfPlaceholderType.Title);
        Assert.Contains(slide.Placeholders, p => p.PlaceholderType == OdfPlaceholderType.Outline);
    }

    /// <summary>
    /// 驗證投影片可建立影片與音訊 plugin 物件。
    /// </summary>
    [Fact]
    public void MediaObjectApiWritesDrawPlugin()
    {
        using var document = PresentationDocument.Create();
        OdfSlide slide = document.AddSlide();

        OdfMediaObject video = slide.AddVideo(
            "Media/demo.mp4",
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("7cm"));
        OdfMediaObject audio = slide.AddAudio(
            "Media/narration.mp3",
            OdfLength.Parse("1cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("1cm"));

        Assert.Equal("Media/demo.mp4", video.PackagePath);
        Assert.Equal("video/mp4", video.MimeType);
        Assert.Equal("audio/mpeg", audio.MimeType);

        string contentXml = SaveAndGetContentXml(document);

        Assert.Contains("draw:plugin", contentXml);
        Assert.Contains("xlink:href=\"Media/demo.mp4\"", contentXml);
        Assert.Contains("draw:mime-type=\"video/mp4\"", contentXml);
        Assert.Contains("xlink:href=\"Media/narration.mp3\"", contentXml);
        Assert.Contains("draw:mime-type=\"audio/mpeg\"", contentXml);
    }

    private static string SaveAndGetContentXml(PresentationDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        return reader.ReadToEnd();
    }
}
