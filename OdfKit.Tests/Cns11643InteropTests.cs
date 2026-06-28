using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 CNS 11643／全字庫情境的 ODT 封裝、分段與字型擴充點。
/// </summary>
public class Cns11643InteropTests
{
    /// <summary>
    /// 驗證高階 API 建立的補充平面文字可 round-trip，並保留全字庫 font-face 與 run 樣式。
    /// </summary>
    [Fact]
    public void AddCns11643Text_RoundTripsFontFacesAndStyledRuns()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string plane2 = char.ConvertFromUtf32(0x20BB7);
        string plane3 = char.ConvertFromUtf32(0x30000);
        string pua = char.ConvertFromUtf32(0xF0000);

        IReadOnlyList<OdfTextRun> runs = paragraph.AddCns11643Text("基" + plane2 + plane3 + pua, "TW-Kai");

        Assert.Equal(4, runs.Count);
        Assert.Equal("TW-Kai", runs[0].FontName);
        Assert.Equal("TW-Kai-Ext-B-98_1", runs[1].FontName);
        Assert.Equal("TW-Kai-98_1", runs[2].FontName);
        Assert.Equal("TW-Kai-Plus-98_1", runs[3].FontName);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        string contentXml = ReadEntry(document.Package, "content.xml");
        string stylesXml = ReadEntry(document.Package, "styles.xml");

        Assert.Contains("TW-Kai-98_1", contentXml, StringComparison.Ordinal);
        Assert.Contains("TW-Kai-Ext-B-98_1", contentXml, StringComparison.Ordinal);
        Assert.Contains("TW-Kai-Plus-98_1", contentXml, StringComparison.Ordinal);
        Assert.Contains("TW-Song-Ext-B-98_1", stylesXml, StringComparison.Ordinal);

        stream.Position = 0;
        using TextDocument loaded = TextDocument.Load(stream);
        Assert.Contains(plane2, loaded.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains(plane3, loaded.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains(pua, loaded.BodyTextRoot.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 PUA 自造字可透過註冊的字型子集化擴充點產生子集字型 entry。
    /// </summary>
    [Fact]
    public void AddCns11643Text_UsesRegisteredSubsetterForPrivateUseRun()
    {
        var subsetter = new FakeFontSubsetter();
        using IDisposable registration = OdfFontResolver.RegisterFontSubsetter(subsetter);
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string pua = char.ConvertFromUtf32(0xF0000);

        paragraph.AddCns11643Text("自造" + pua, "TW-Kai");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);

        Assert.Contains(subsetter.Requests, request => request.FontName == "TW-Kai-Plus-98_1" && request.CodePoints.Contains(0xF0000));
        Assert.True(document.Package.HasEntry("Fonts/Subsets/TW-Kai-Plus-98_1-subset.ttf"));
        Assert.Equal("font/ttf", document.Package.Manifest["Fonts/Subsets/TW-Kai-Plus-98_1-subset.ttf"]);
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FakeFontSubsetter : IFontSubsetter
    {
        public List<OdfFontSubsetRequest> Requests { get; } = [];

        public OdfFontSubset? CreateSubset(OdfFontSubsetRequest request)
        {
            Requests.Add(request);
            return new OdfFontSubset([0x00, 0x01, 0x02], ".ttf", "font/ttf");
        }
    }
}
