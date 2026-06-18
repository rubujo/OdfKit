using System.IO;
using System.Text;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

// Native AOT / trimming 煙霧測試：觸及主要公開 API 根，供 IL 連結器驗證（PERF-5e）。
using var stream = new MemoryStream();
using var document = SpreadsheetDocument.Create();
document.AddSheet("Sheet1");
document.SaveToStream(stream);

stream.Position = 0;
using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
using Stream contentStream = package.GetEntryStream("content.xml");
OdfNode root = OdfXmlReader.Parse(contentStream);

using var chartStream = new MemoryStream();
using var chartDoc = ChartDocument.Create();
chartDoc.SaveToStream(chartStream);

Console.WriteLine($"TrimSmoke OK: root={root.LocalName}, sheets={document.GetSheets().Count}");