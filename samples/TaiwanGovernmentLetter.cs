#:project ../OdfKit/OdfKit.csproj

using OdfKit;
using OdfKit.Text;

string outputDirectory = Environment.GetEnvironmentVariable("ODFKIT_SAMPLE_OUTPUT_DIR")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "samples", "output");
Directory.CreateDirectory(outputDirectory);

string templatePath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(outputDirectory, "government-letter-reference.ott");
string outputPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(outputDirectory, "government-letter-example.odt");

if (args.Length == 0)
{
    CreateReferenceTemplate(templatePath);
}

using TextTemplateDocument template = TextTemplateDocument.Load(templatePath);
using TextDocument document = TextDocument.CreateFromTemplate(template);

var values = new Dictionary<string, object?>
{
    ["機關全銜"] = "範例機關",
    ["發文日期"] = "中華民國 115 年 7 月 13 日",
    ["發文字號"] = "範例字第 1150000001 號",
    ["速別"] = "普通件",
    ["密等"] = "普通",
    ["附件"] = "無",
    ["主旨"] = "檢送 OdfKit 公文 ODT 範例，請查照。",
    ["說明"] = "本範例展示如何以外部 OTT 範本產生 ODT 檔案。",
    ["正本"] = "範例受文機關",
    ["副本"] = "本機關資訊單位",
};

foreach ((string name, object? value) in values)
{
    document.SetFieldValue(name, value?.ToString());
}

TemplateBinder.Bind(document, values);
document.Metadata.Title = "公文 ODT 範例";
document.Metadata.Subject = values["主旨"]?.ToString();
document.SetCustomProperty("Example.DocumentType", "函");
document.Save(outputPath);

Console.WriteLine($"已產生公文 ODT 範例：{outputPath}");
Console.WriteLine("本範例僅示範 ODF 範本繫結，不代表任何機關的正式公文版面或電子交換合規認證。");

static void CreateReferenceTemplate(string path)
{
    using TextTemplateDocument template = TextTemplateDocument.Create();
    template.AddParagraph("{{機關全銜}}　函");
    template.AddParagraph("發文日期：{{發文日期}}");
    template.AddParagraph("發文字號：{{發文字號}}");
    template.AddParagraph("速別：{{速別}}");
    template.AddParagraph("密等及解密條件或保密期限：{{密等}}");
    template.AddParagraph("附件：{{附件}}");
    template.AddParagraph("主旨：{{主旨}}");
    template.AddParagraph("說明：{{說明}}");
    template.AddParagraph("正本：{{正本}}");
    template.AddParagraph("副本：{{副本}}");
    template.Save(path);
}
