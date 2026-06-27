using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Extensions.Rendering;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Interop)]
public class LibreOfficeConversionApiTests
{
    [Theory]
    [InlineData(OdfDocumentKind.Text, "odt")]
    [InlineData(OdfDocumentKind.TextTemplate, "ott")]
    [InlineData(OdfDocumentKind.TextMaster, "odm")]
    [InlineData(OdfDocumentKind.Spreadsheet, "ods")]
    [InlineData(OdfDocumentKind.SpreadsheetTemplate, "ots")]
    [InlineData(OdfDocumentKind.Presentation, "odp")]
    [InlineData(OdfDocumentKind.PresentationTemplate, "otp")]
    [InlineData(OdfDocumentKind.Graphics, "odg")]
    [InlineData(OdfDocumentKind.GraphicsTemplate, "otg")]
    [InlineData(OdfDocumentKind.Chart, "odc")]
    [InlineData(OdfDocumentKind.Formula, "odf")]
    [InlineData(OdfDocumentKind.Image, "odi")]
    [InlineData(OdfDocumentKind.Database, "odb")]
    [InlineData(OdfDocumentKind.FlatText, "fodt")]
    [InlineData(OdfDocumentKind.FlatSpreadsheet, "fods")]
    [InlineData(OdfDocumentKind.FlatPresentation, "fodp")]
    [InlineData(OdfDocumentKind.FlatGraphics, "fodg")]
    [InlineData(OdfDocumentKind.FlatChart, "fodc")]
    [InlineData(OdfDocumentKind.FlatFormula, "fdf")]
    [InlineData(OdfDocumentKind.FlatImage, "fodi")]
    public void GetInputExtensionUsesOdfFormatTable(OdfDocumentKind kind, string expectedExtension)
    {
        using OdfDocument document = OdfDocument.Create(kind);

        Assert.Equal(expectedExtension, LibreOfficeRenderer.GetInputExtension(document));
    }

    [Fact]
    public async Task HttpRendererPassesFullOdfInputExtensionToBackend()
    {
        var backend = new CapturingBackend();
        using var renderer = new LibreOfficeHttpRenderer(backend);
        using OdfDocument document = OdfDocument.Create(OdfDocumentKind.Graphics);
        using var output = new MemoryStream();

        await renderer.ConvertAsync(document, output, LibreOfficeConversionFormats.Svg, TestContext.Current.CancellationToken);

        Assert.Equal("odg", backend.InputExtension);
        Assert.Equal(LibreOfficeConversionFormats.Svg, backend.ConvertTo);
        Assert.True(backend.InputLength > 0);
        Assert.Equal("converted", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task ConvertToLibreOfficeFormatAsyncUsesSuppliedRendererAndFormat()
    {
        using var document = TextDocument.Create();
        var renderer = new CapturingRenderer();
        string outputPath = Path.Combine(Path.GetTempPath(), "OdfKit_Api_" + Guid.NewGuid().ToString("N") + ".md");

        await document.ConvertToLibreOfficeFormatAsync(
            outputPath,
            LibreOfficeConversionFormats.Markdown,
            renderer,
            TestContext.Current.CancellationToken);

        Assert.Same(document, renderer.Document);
        Assert.Equal(outputPath, renderer.OutputPath);
        Assert.Equal(LibreOfficeConversionFormats.Markdown, renderer.Format);
        Assert.True(renderer.CancellationToken.CanBeCanceled);
    }

    [Fact]
    public void LibreOfficeFallbackMethodsUseStableFormats()
    {
        using var document = TextDocument.Create();
        var calls = new List<string>();
        var renderer = new CapturingRenderer((_, _, format, _) => calls.Add(format));

        document.ConvertToPdf("out.pdf", renderer);
        document.ConvertToLibreOfficeFormat("out.pptx", LibreOfficeConversionFormats.Pptx, renderer);

        Assert.Equal(
            new[]
            {
                LibreOfficeConversionFormats.Pdf,
                LibreOfficeConversionFormats.Pptx
            },
            calls);
    }

    [Fact]
    public void DefaultPathHonorsPortableLibreOfficeDirectoryFromEnvironment()
    {
        string root = Path.Combine(Path.GetTempPath(), "OdfKit_PortableLO_" + Guid.NewGuid().ToString("N"));
        string programDir = Path.Combine(root, "App", "libreoffice", "program");
        string sofficePath = Path.Combine(programDir, "soffice.com");
        string? originalOdfKit = Environment.GetEnvironmentVariable("ODFKIT_SOFFICE_PATH");
        string? originalLibreOffice = Environment.GetEnvironmentVariable("LIBREOFFICE_PATH");

        try
        {
            Directory.CreateDirectory(programDir);
            File.WriteAllText(sofficePath, string.Empty);
            Environment.SetEnvironmentVariable("ODFKIT_SOFFICE_PATH", root);
            Environment.SetEnvironmentVariable("LIBREOFFICE_PATH", null);

            var renderer = new LibreOfficeRenderer();

            Assert.Equal(sofficePath, renderer.LibreOfficePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ODFKIT_SOFFICE_PATH", originalOdfKit);
            Environment.SetEnvironmentVariable("LIBREOFFICE_PATH", originalLibreOffice);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class CapturingBackend : ILibreOfficeConversionBackend
    {
        public string? InputExtension { get; private set; }

        public string? ConvertTo { get; private set; }

        public long InputLength { get; private set; }

        public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
        {
            InputExtension = inputExtension;
            ConvertTo = convertTo;

            using var sink = new MemoryStream();
            await input.CopyToAsync(sink, 81920, ct).ConfigureAwait(false);
            InputLength = sink.Length;

            return new MemoryStream(Encoding.UTF8.GetBytes("converted"));
        }
    }

    private sealed class CapturingRenderer : LibreOfficeRenderer
    {
        private readonly Action<OdfDocument, string, string, CancellationToken>? _onConvert;

        public CapturingRenderer()
        {
        }

        public CapturingRenderer(Action<OdfDocument, string, string, CancellationToken> onConvert)
        {
            _onConvert = onConvert;
        }

        public OdfDocument? Document { get; private set; }

        public string? OutputPath { get; private set; }

        public string? Format { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public override Task ConvertAsync(
            OdfDocument document,
            string outputPath,
            string format,
            CancellationToken cancellationToken = default)
        {
            Document = document;
            OutputPath = outputPath;
            Format = format;
            CancellationToken = cancellationToken;
            _onConvert?.Invoke(document, outputPath, format, cancellationToken);
            return Task.CompletedTask;
        }
    }
}
