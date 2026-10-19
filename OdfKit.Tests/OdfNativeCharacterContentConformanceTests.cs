using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests native character data storage independently of field computation and element-specific structure.
/// 測試原生字元資料保存，並與欄位計算及個別元素結構分開處理。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfNativeCharacterContentConformanceTests
{
    // 版本遮罩 1/2/4 對應 1.2/1.3/1.4；只依各版實際 character data 句子列入。
    private static readonly (string Name, int Versions)[] SourceCases =
    [
        ("config:config-item", 7), // ODFG-8dea8d770515c13949ec
        ("form:item", 7), // ODFG-a40847edee751524f678
        ("form:option", 7), // ODFG-92ef04b7f42c549e4dd0
        ("meta:creator-initials", 6), // ODFG-267da1d3e3627d9e5c9c
        ("meta:user-defined", 7), // ODFG-9c53acac2cd5ecd4a765
        ("number:currency-symbol", 7), // ODFG-4b2d94fceb8addd96590
        ("number:embedded-text", 7), // ODFG-5ceb4326c29a4834085b
        ("number:fill-character", 6), // ODFG-abdf2132bb3942475345
        ("number:text", 7), // ODFG-0a7d7ec678c80730fbbe
        ("presentation:date-time-decl", 7), // ODFG-e4a33344101744d606e4
        ("presentation:footer-decl", 7), // ODFG-2294554d098652ab6b90
        ("presentation:header-decl", 7), // ODFG-15c886459f97df9400e1
        ("svg:desc", 7), // ODFG-2f92fc7f526c3e070ea3
        ("svg:title", 7), // ODFG-82902b355b807a1a5414
        ("table:desc", 7), // ODFG-17eb48f3d5c965545544
        ("table:title", 7), // ODFG-f0317136173ad955403b
        ("text:author-initials", 7), // ODFG-4c4ad98731f406a3a52e
        ("text:author-name", 7), // ODFG-2471f7b94a4026b84337
        ("text:bibliography-mark", 7), // ODFG-8da76246b91da84ed8b9
        ("text:bookmark-ref", 1), // ODFG-6c156a888fde9bbaa290
        ("text:chapter", 7), // ODFG-272c56e640e53d6784ee
        ("text:character-count", 7), // ODFG-138bb1fc57b2d0ed8ceb
        ("text:conditional-text", 7), // ODFG-2e719adcee4a025d69e9
        ("text:creation-date", 7), // ODFG-3598109f0b725941665b
        ("text:creation-time", 7), // ODFG-e7bc342a5df09cf1c9f1
        ("text:creator", 7), // ODFG-d7e4401c9849a6a7638f
        ("text:database-display", 7), // ODFG-b607d4ae30e71ba4135b
        ("text:database-name", 7), // ODFG-2334fd4d9ced128f7f8f
        ("text:database-row-number", 7), // ODFG-6ff8cd4593e22bca682c
        ("text:date", 7), // ODFG-4e791058c1612dd460ea
        ("text:dde-connection", 7), // ODFG-37cb5fe2daf4599d4f75
        ("text:description", 7), // ODFG-e6f9fbf6e6c60ca9defa
        ("text:drop-down", 6), // ODFG-1eb0105f48edf6e9a766
        ("text:editing-cycles", 7), // ODFG-43da89ba8adf039970fc
        ("text:editing-duration", 7), // ODFG-361417cc890a13865db8
        ("text:execute-macro", 7), // ODFG-77c19ca1d7d4af1e4c66
        ("text:expression", 7), // ODFG-6ea3cf11bd2ca8e7e0b9
        ("text:file-name", 7), // ODFG-1b18162846534ce68701
        ("text:hidden-paragraph", 7), // ODFG-a8a68784813b8b127a87
        ("text:hidden-text", 7), // ODFG-d2c3e6c89a11fe46a556
        ("text:image-count", 7), // ODFG-4adf5c9cba9367882521
        ("text:index-entry-span", 7), // ODFG-f953dfe3a4a54727cdfa
        ("text:index-title-template", 7), // ODFG-338467dd09a0545e388f
        ("text:initial-creator", 7), // ODFG-d20631dfcf5b2eb4d1b6
        ("text:keywords", 7), // ODFG-e6b92f6760c44277ca72
        ("text:linenumbering-separator", 7), // ODFG-25f776ed24d2cb65e1c1
        ("text:measure", 7), // ODFG-7950a6d6e732cec8daef
        ("text:modification-date", 7), // ODFG-d29fc1c80de99a05c3af
        ("text:modification-time", 7), // ODFG-579086629d31df01aca4
        ("text:note-citation", 7), // ODFG-6fc0b923fa9031b618e4
        ("text:note-continuation-notice-backward", 7), // ODFG-4976e49d0178743b8773
        ("text:note-continuation-notice-forward", 7), // ODFG-13be9c6e391d6db38768
        ("text:note-ref", 1), // ODFG-2898c7d813c35c74347b
        ("text:object-count", 7), // ODFG-cd37729188a8a2f108f4
        ("text:page-continuation", 7), // ODFG-1eb3b93228a21c9fa967
        ("text:page-count", 7), // ODFG-400fb37553f6b2631737
        ("text:page-number", 7), // ODFG-f603ef9965b344736cc1
        ("text:page-variable-get", 7), // ODFG-02e5cb5cf15238aca475
        ("text:page-variable-set", 7), // ODFG-3bb86233f0eb2b3cd62e
        ("text:paragraph-count", 7), // ODFG-779cf9d9ef3c8edcc262
        ("text:placeholder", 7), // ODFG-e7874065c66983f9f803
        ("text:print-date", 7), // ODFG-677dbbb8d1eb523c7056
        ("text:print-time", 7), // ODFG-397062085057038b9ef7
        ("text:printed-by", 7), // ODFG-9af0c3b76a37b79dd7cc
        ("text:reference-ref", 1), // ODFG-749a0a6115ea33ecfe3a
        ("text:ruby-text", 7), // ODFG-41596eda144f06e5efee
        ("text:script", 7), // ODFG-42349b88b77f18360178
        ("text:sender-city", 7), // ODFG-c02b7f584b00019abc6d
        ("text:sender-company", 7), // ODFG-d88f901b0e6d794eee84
        ("text:sender-country", 7), // ODFG-b4acaf71bab20bd35c0f
        ("text:sender-email", 7), // ODFG-2f37ba76004677c9a95f
        ("text:sender-fax", 7), // ODFG-0918ebd769f76ec36f4d
        ("text:sender-firstname", 7), // ODFG-e031575b5e56a673ac66
        ("text:sender-initials", 7), // ODFG-90986d068ebc9b3c6d73
        ("text:sender-lastname", 7), // ODFG-074dd0ef9545f3817936
        ("text:sender-phone-private", 7), // ODFG-4a04156c7be90b9e18fe
        ("text:sender-phone-work", 7), // ODFG-6e6167ebb35441ece7fe
        ("text:sender-position", 7), // ODFG-31efd1bf45a19d4ede32
        ("text:sender-postal-code", 7), // ODFG-56bd8ae824f3944d928c
        ("text:sender-state-or-province", 7), // ODFG-9dea97cdc86f7885a1b9
        ("text:sender-street", 7), // ODFG-b441f6aaf8b3f9629afe
        ("text:sender-title", 7), // ODFG-d6ba953fd01e40a25150
        ("text:sequence", 7), // ODFG-95d7be41cb497da92194
        ("text:sequence-ref", 1), // ODFG-045dbaa3c4e066f8cc93
        ("text:sheet-name", 7), // ODFG-8c802d201c5fa75aef72
        ("text:subject", 7), // ODFG-dbc8ea0c2ecfae2a9e3b
        ("text:table-count", 7), // ODFG-7b8e3835029119436679
        ("text:table-formula", 7), // ODFG-47bb280604b51b5165fe
        ("text:template-name", 7), // ODFG-2fb355be2db1d8c1311d
        ("text:text-input", 7), // ODFG-e5c528cba52a9f8368a1
        ("text:time", 7), // ODFG-bc73df14f4afb5d9b992
        ("text:title", 7), // ODFG-9568ce9d955f1c4dfa11
        ("text:user-defined", 7), // ODFG-375348523a163332d018
        ("text:user-field-get", 7), // ODFG-abfdea5abd5cd3064073
        ("text:user-field-input", 7), // ODFG-8a22a80f5293a92aca73
        ("text:variable-get", 7), // ODFG-8da52e8be838fddaf89f
        ("text:variable-input", 7), // ODFG-2d80c282e4aee1e6a6e6
        ("text:variable-set", 7), // ODFG-9ee5e8d448211877824e
        ("text:word-count", 7), // ODFG-0eb3424f36ec05e4701f
    ];

    private static readonly string[] Values = [" ", "\t", "\n", " \t\r\n ", "文字😀<&>"];
    private static readonly string[] LegacyCases = ["meta:user-defined", "number:text", "text:sender-firstname", "text:placeholder", "text:creation-date", "presentation:header-decl", "config:config-item"];

    /// <summary>
    /// Source-declared character data remains intact through native fragment readers and writers.
    /// 來源明定的字元資料經原生片段讀寫後保持完整。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SourceDeclaredCharacterDataSurvivesReading(OdfVersion version)
    {
        int mask = version switch { OdfVersion.Odf12 => 1, OdfVersion.Odf13 => 2, _ => 4 };
        int exercised = 0;
        foreach (var sample in SourceCases)
        {
            if ((sample.Versions & mask) == 0)
                continue;
            CheckCharacterData(sample.Name);
            exercised++;
        }
        Assert.Equal(version == OdfVersion.Odf12 ? 96 : 95, exercised);
    }

    /// <summary>
    /// Legacy native string carriers preserve whitespace without requiring modern schema initialization.
    /// 舊版原生字串元素保留空白，且不需要初始化現代 schema。
    /// </summary>
    [Fact]
    public void LegacyNativeCharacterDataSurvivesReading()
    {
        foreach (string name in LegacyCases)
            CheckCharacterData(name);
    }

    /// <summary>
    /// Element-only structural containers continue to discard indentation whitespace.
    /// 只含元素的結構容器繼續略過縮排空白。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void StructuralIndentationDoesNotBecomeCharacterData(OdfVersion version)
    {
        string xml = $"<office:document xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' office:version='{OdfVersionInfo.ToVersionString(version)}'>\n <office:body>\n <office:text>\n <text:p>visible</text:p>\n </office:text>\n </office:body>\n </office:document>";
        foreach (bool streamReader in new[] { false, true })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(xml);
            using MemoryStream input = new(bytes);
            OdfNode root = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            OdfNode body = Assert.Single(root.Children);
            OdfNode text = Assert.Single(body.Children);
            OdfNode paragraph = Assert.Single(text.Children);
            Assert.Equal("visible", paragraph.TextContent);
        }
    }

    private static void CheckCharacterData(string name)
    {
        int colon = name.IndexOf(':');
        string ns = name[..colon] switch
        {
            "text" => OdfNamespaces.Text,
            "config" => OdfNamespaces.Config,
            "meta" => OdfNamespaces.Meta,
            "number" => OdfNamespaces.Number,
            "presentation" => OdfNamespaces.Presentation,
            "table" => OdfNamespaces.Table,
            "svg" => OdfNamespaces.Svg,
            "form" => OdfNamespaces.Form,
            _ => throw new InvalidOperationException()
        };
        string localName = name[(colon + 1)..];
        foreach (string value in Values)
            foreach (bool streamReader in new[] { false, true })
            {
                string escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", "&#xD;");
                byte[] bytes = Encoding.UTF8.GetBytes($"<q:{localName} xmlns:q='{ns}'>{escaped}</q:{localName}>");
                using MemoryStream input = new(bytes);
                OdfNode node = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
                Assert.Equal((name, value), (name, node.TextContent));
                using MemoryStream output = new();
                OdfXmlWriter.Write(node, output);
                XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
                Assert.Equal(XName.Get(localName, ns), saved.Name);
                Assert.Equal((name, value), (name, saved.Value));
            }
    }
}
