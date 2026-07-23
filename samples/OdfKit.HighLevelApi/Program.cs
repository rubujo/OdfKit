using System.Text;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;

Console.OutputEncoding = Encoding.UTF8;

using TextDocument host = TextDocument.Create();
host.AddParagraph("OdfKit high-level API sample");
host.AddParagraph("Extract text and manage embedded documents through high-level APIs.");

using SpreadsheetDocument embedded = SpreadsheetDocument.Create();
embedded.Worksheets.Add("Data");
embedded.SetValue("Data", "A1", "Embedded spreadsheet");
host.Package.AddEmbeddedDocument("Object 1", embedded);

using var package = new MemoryStream(host.SaveToBytes());
using TextDocument reopened = TextDocument.Load(package);

Console.WriteLine(reopened.ExtractText());
foreach (OdfEmbeddedObjectInfo item in reopened.Package.GetEmbeddedObjectInfos())
{
    Console.WriteLine($"{item.Path}: {item.DocumentKind} ({item.MediaType})");
}
