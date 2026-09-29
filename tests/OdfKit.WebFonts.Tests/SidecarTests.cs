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

    private static byte[] CreateFrameHeader(int declaredPayloadLength)
    {
        // 與 SidecarProtocol 相同的 12 位元組標頭：magic、版本、操作、狀態、酬載長度（小端序）。
        byte[] header = new byte[SidecarProtocol.HeaderLength];
        BitConverter.GetBytes(0x5746444Fu).CopyTo(header, 0);
        BitConverter.GetBytes(SidecarProtocol.Version).CopyTo(header, 4);
        header[6] = (byte)SidecarOperation.Health;
        header[7] = (byte)SidecarStatus.Success;
        BitConverter.GetBytes(declaredPayloadLength).CopyTo(header, 8);
        return header;
    }

    [Fact]
    public void FrameReaderDoesNotAllocateTheDeclaredPayloadBeforeItArrives()
    {
        // 未認證的連線只送標頭並宣告 16 MB：修正前會先配置 16 MB 才等待資料。
        const int declared = 16 * 1024 * 1024;
        using var stream = new MemoryStream(CreateFrameHeader(declared));
        long before = GC.GetAllocatedBytesForCurrentThread();

        // MemoryStream 同步完成，整個流程停留在目前的執行緒，配置量可準確量測。
        Assert.Throws<EndOfStreamException>(() => SidecarProtocol
            .ReadRequestFrameAsync(stream, declared, CancellationToken.None)
            .GetAwaiter()
            .GetResult());

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"配置了 {allocated} 位元組。");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64 * 1024)]
    [InlineData(64 * 1024 + 1)]
    [InlineData(300_000)]
    public async Task FrameReaderRoundTripsPayloadsAcrossChunkBoundaries(int length)
    {
        byte[] payload = new byte[length];
        new Random(length).NextBytes(payload);
        using var buffer = new MemoryStream();
        await SidecarProtocol.WriteRequestFrameAsync(
            buffer,
            SidecarOperation.Health,
            payload,
            TestContext.Current.CancellationToken);
        buffer.Position = 0;

        SidecarFrame frame = await SidecarProtocol.ReadRequestFrameAsync(
            buffer,
            1024 * 1024,
            TestContext.Current.CancellationToken);

        Assert.Equal(payload, frame.Payload);
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
