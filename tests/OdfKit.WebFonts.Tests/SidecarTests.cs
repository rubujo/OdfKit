using OdfKit.WebFonts.Sidecar;
using OdfKit.WebFonts.Sidecar.Server;

namespace OdfKit.WebFonts.Tests;

public sealed class SidecarTests
{
    [Fact]
    public void ProtocolRejectsOversizedFieldsBeforeSerializingPayload()
    {
        var request = new WebFontSubsetRequest
        {
            Face = CreateFace(),
            ProfileId = "sidecar-test@1",
            FontFamily = new string('x', 1025),
            Sequences = [WebFontTextSequence.Create("A")],
            Formats = [WebFontFormat.Woff2]
        };

        Assert.Throws<ArgumentException>(() => SidecarProtocol.CreateGenerateRequest(
            new string('a', 64),
            request));
    }

    [Fact]
    public async Task AuthenticatedClientNegotiatesAndDelegatesOperations()
    {
        string root = Path.Combine(Path.GetTempPath(), "odfkit-sidecar-test-" + Guid.NewGuid().ToString("N"));
        string pipeName = "odfkit-sidecar-" + Guid.NewGuid().ToString("N");
        string token = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        Directory.CreateDirectory(root);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var server = new WebFontSidecarServer(
            new SidecarSmokeEngine(),
            new WebFontSidecarServerOptions
            {
                PipeName = pipeName,
                AuthenticationToken = token,
                AssetRootPath = root,
                MaxMessageBytes = 1024 * 1024,
                MaxConnections = 4,
                ConnectionTimeout = TimeSpan.FromSeconds(5),
                CurrentUserOnly = true,
                IsWoff2Available = true,
                RuntimeIdentifier = "test-x64"
            });
        Task serverTask = server.RunAsync(shutdown.Token);

        try
        {
            var client = new OdfWebFontSidecarClient(new WebFontSidecarClientOptions
            {
                PipeName = pipeName,
                AuthenticationToken = token,
                AssetRootPath = root,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                RequestTimeout = TimeSpan.FromSeconds(15)
            });
            WebFontSidecarHealth health = await client.GetHealthAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(1, health.ProtocolVersion);
            Assert.True(health.IsWoff2Available);
            Assert.Equal("test-x64", health.RuntimeIdentifier);

            WebFontTextSequence sequence = WebFontTextSequence.Create("A𠆩\uFE00");
            IReadOnlyList<WebFontTextSequence> supported = await client.FilterSupportedSequencesAsync(
                CreateFace(),
                [sequence],
                TestContext.Current.CancellationToken);
            Assert.Equal(sequence.Text, Assert.Single(supported).Text);

            WebFontManifest manifest = await client.GenerateAsync(
                new WebFontSubsetRequest
                {
                    Face = CreateFace(),
                    ProfileId = "sidecar-test@1",
                    FontFamily = "OdfKit Sidecar Test",
                    Sequences = [sequence],
                    Formats = [WebFontFormat.Woff2]
                },
                root,
                TestContext.Current.CancellationToken);
            WebFontAsset asset = Assert.Single(manifest.Assets);
            Assert.Equal(WebFontFormat.Woff2, asset.Format);
            Assert.Equal("sidecar-test.woff2", asset.FileName);
        }
        finally
        {
            shutdown.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => serverTask);
            await server.DrainAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SidecarRejectsAnInvalidAuthenticationToken()
    {
        string root = Path.Combine(Path.GetTempPath(), "odfkit-sidecar-auth-" + Guid.NewGuid().ToString("N"));
        string pipeName = "odfkit-sidecar-" + Guid.NewGuid().ToString("N");
        string token = new('a', 64);
        Directory.CreateDirectory(root);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var server = new WebFontSidecarServer(
            new SidecarSmokeEngine(),
            new WebFontSidecarServerOptions
            {
                PipeName = pipeName,
                AuthenticationToken = token,
                AssetRootPath = root,
                MaxMessageBytes = 1024 * 1024,
                MaxConnections = 2,
                ConnectionTimeout = TimeSpan.FromSeconds(5),
                CurrentUserOnly = true,
                IsWoff2Available = true,
                RuntimeIdentifier = "test-x64"
            });
        Task serverTask = server.RunAsync(shutdown.Token);

        try
        {
            var client = new OdfWebFontSidecarClient(new WebFontSidecarClientOptions
            {
                PipeName = pipeName,
                AuthenticationToken = new string('b', 64),
                AssetRootPath = root
            });
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => client.GetHealthAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            shutdown.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => serverTask);
            await server.DrainAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private static WebFontFaceIdentity CreateFace()
        => new()
        {
            FontSourceId = "sidecar-test",
            SourceSha256 = new string('a', 64),
            FaceIndex = 0
        };

    private sealed class SidecarSmokeEngine : IWebFontSubsetEngine, IWebFontTextCoverageFilter
    {
        public Task<WebFontManifest> GenerateAsync(
            WebFontSubsetRequest request,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WebFontManifest
            {
                ProfileId = request.ProfileId,
                Assets =
                [
                    new WebFontAsset
                    {
                        FileName = "sidecar-test.woff2",
                        Sha256 = new string('b', 64),
                        ByteLength = 128,
                        Format = WebFontFormat.Woff2,
                        FontFamily = request.FontFamily,
                        UnicodeRanges = ["U+41", "U+FE00", "U+20189"]
                    }
                ]
            });
        }

        public Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
            WebFontFaceIdentity face,
            IReadOnlyList<WebFontTextSequence> sequences,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(sequences);
        }
    }
}
