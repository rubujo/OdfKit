using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 Flat XML 的二進位媒體串流編解碼效能與功能 Round-trip 測試。
/// </summary>
public class FlatXmlMediaEncodingTests
{
    /// <summary>
    /// 驗證大型圖片檔案載入與另存為 Flat XML 時的 Base64 串流編解碼與無損讀寫。
    /// </summary>
    [Fact]
    public void LargeMediaFlatXmlStreamingRoundTrip()
    {
        byte[] largeImageBytes = new byte[5 * 1024 * 1024];
        new Random(42).NextBytes(largeImageBytes);

        using var doc = TextDocument.Create();
        doc.Package.WriteEntry("Pictures/image_large.png", largeImageBytes, "image/png");

        var body = doc.ContentDom.Children.First(c => c.LocalName == "body" && c.NamespaceUri == OdfNamespaces.Office);
        var textNode = body.Children.First(c => c.LocalName == "text" && c.NamespaceUri == OdfNamespaces.Office);

        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        var imageNode = new OdfNode(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
        imageNode.SetAttribute("href", OdfNamespaces.XLink, "Pictures/image_large.png", "xlink");

        frame.AppendChild(imageNode);
        paragraph.AppendChild(frame);
        textNode.AppendChild(paragraph);

        string tempFlatXmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"temp_flat_xml_{Guid.NewGuid():N}.fodt");
        try
        {
            doc.SaveAsFlatXml(tempFlatXmlPath);

            var fileInfo = new FileInfo(tempFlatXmlPath);
            Assert.True(fileInfo.Length > 5 * 1024 * 1024);

            using var loadedDoc = OdfDocument.LoadFromFlatXml(tempFlatXmlPath);

            Assert.True(loadedDoc.Package.HasEntry("Pictures/image_1.bin"));
            byte[] extractedBytes = loadedDoc.Package.ReadEntry("Pictures/image_1.bin");

            Assert.Equal(largeImageBytes.Length, extractedBytes.Length);
            Assert.Equal(largeImageBytes[0], extractedBytes[0]);
            Assert.Equal(largeImageBytes[largeImageBytes.Length - 1], extractedBytes[largeImageBytes.Length - 1]);
        }
        finally
        {
            if (File.Exists(tempFlatXmlPath))
                File.Delete(tempFlatXmlPath);
        }
    }
}
