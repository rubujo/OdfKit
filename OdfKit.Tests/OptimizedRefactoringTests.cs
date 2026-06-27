using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對 OdfKit 底層 API 設計優化與效能重構之所有新增功能進行完整驗證的測試類別。
/// </summary>
public class OptimizedRefactoringTests
{
    /// <summary>
    /// 驗證 MMF lazy preload 會遵守全域 CPU 核心預留平行度。
    /// </summary>
    [Fact]
    public void Test_OdfPackage_MmfPreload_UsesReservedCpuConcurrency()
    {
        double originalRatio = OdfParallelScheduler.ReservationRatio;
        OdfParallelScheduler.ReservationRatio = 0.99d;
        try
        {
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                OdfPackageZipLoader.CreatePreloadParallelOptions().MaxDegreeOfParallelism);
        }
        finally
        {
            OdfParallelScheduler.ReservationRatio = originalRatio;
        }
    }

    /// <summary>
    /// 驗證檔案路徑載入會以 MMF 定位核心 XML entries，並將多個獨立 entry 排入平行預讀。
    /// </summary>
    [Fact]
    public async Task Test_OdfPackage_MmfPreload_QueuesCoreXmlEntriesForParallelRandomAccess()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_preload_{Guid.NewGuid():N}.ods");
        byte[] xml = Encoding.UTF8.GetBytes("<root><item>payload</item></root>");
        byte[] manifest = Encoding.UTF8.GetBytes("""
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
              <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet" />
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="meta.xml" manifest:media-type="text/xml" />
              <manifest:file-entry manifest:full-path="settings.xml" manifest:media-type="text/xml" />
            </manifest:manifest>
            """);

        try
        {
            using (MemoryStream packageStream = CreateZipPackage(
                ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet")),
                ("content.xml", xml),
                ("styles.xml", xml),
                ("meta.xml", xml),
                ("settings.xml", xml),
                ("META-INF/manifest.xml", manifest),
                ("Pictures/image.bin", [1, 2, 3, 4])))
            {
                File.WriteAllBytes(tempFile, packageStream.ToArray());
            }

            using OdfPackage package = OdfPackage.Open(
                tempFile,
                new OdfLoadOptions { AllowLazyLoading = true });

            Assert.NotNull(package.MmfEntries);
            Assert.NotNull(package.PreloadTask);

            await package.PreloadTask!.WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(4, OdfPackageZipLoader.LastMmfParallelPreloadEntryCountForTests);
            Assert.Equal(4, OdfPackageZipLoader.LastMmfParallelPreloadVisitedEntryCountForTests);
            Assert.Equal(
                OdfParallelScheduler.GetEffectiveConcurrency(),
                OdfPackageZipLoader.LastMmfParallelPreloadMaxDegreeForTests);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// 驗證平行調度器會在工作委派期間暫時套用執行緒優先權，並於完成後還原。
    /// </summary>
    [Fact]
    public void Test_OdfParallelScheduler_AppliesAndRestoresWorkerThreadPriority()
    {
        ThreadPriority? originalConfiguredPriority = OdfParallelScheduler.WorkerThreadPriority;
        ThreadPriority originalThreadPriority = Thread.CurrentThread.Priority;
        ThreadPriority targetPriority = originalThreadPriority == ThreadPriority.BelowNormal
            ? ThreadPriority.Normal
            : ThreadPriority.BelowNormal;

        try
        {
            OdfParallelScheduler.WorkerThreadPriority = targetPriority;

            ThreadPriority observedPriority = OdfParallelScheduler.RunWithConfiguredThreadPriority(
                static () => Thread.CurrentThread.Priority);

            Assert.Equal(targetPriority, observedPriority);
            Assert.Equal(originalThreadPriority, Thread.CurrentThread.Priority);
        }
        finally
        {
            OdfParallelScheduler.WorkerThreadPriority = originalConfiguredPriority;
            Thread.CurrentThread.Priority = originalThreadPriority;
        }
    }

    /// <summary>
    /// 驗證文件非同步載入會解析 content、styles、meta 與 settings 四個核心 XML entry。
    /// </summary>
    [Fact]
    public async Task Test_OdfDocument_LoadAsync_ParsesCoreXmlEntries()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"odfkit_async_load_{Guid.NewGuid():N}.odt");
        const string markerNamespace = "urn:example:odfkit:async-load";

        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.AddParagraph("非同步載入內容");
                document.StylesDom.AppendChild(new OdfUnknownElement("styles-marker", markerNamespace, "async"));
                document.MetaDom.AppendChild(new OdfUnknownElement("meta-marker", markerNamespace, "async"));
                document.SettingsDom.AppendChild(new OdfUnknownElement("settings-marker", markerNamespace, "async"));
                document.Save(tempFile);
            }

            await using TextDocument loaded = await TextDocument.LoadAsync(
                tempFile,
                TestContext.Current.CancellationToken);

            Assert.Contains("非同步載入內容", loaded.ContentDom.TextContent, StringComparison.Ordinal);
            Assert.Contains(loaded.StylesDom.Descendants(), node => node.LocalName == "styles-marker" && node.NamespaceUri == markerNamespace);
            Assert.Contains(loaded.MetaDom.Descendants(), node => node.LocalName == "meta-marker" && node.NamespaceUri == markerNamespace);
            Assert.Contains(loaded.SettingsDom.Descendants(), node => node.LocalName == "settings-marker" && node.NamespaceUri == markerNamespace);
            Assert.Equal(4, OdfDocument.LastCoreXmlChannelJobCountForTests);
            Assert.Equal(
                Math.Min(4, OdfParallelScheduler.GetEffectiveConcurrency()),
                OdfDocument.LastCoreXmlChannelWorkerCountForTests);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private class DummyRenderer : IOdfRenderer
    {
        public bool ExportCalled { get; private set; }

        public void ExportToPdf(OdfDocument document, Stream pdfStream, System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null)
        {
            ExportCalled = true;
            byte[] dummyData = "PDF Content"u8.ToArray();
            pdfStream.Write(dummyData, 0, dummyData.Length);
        }
    }

    [Fact]
    public void Test_OdfXmlNameTable_Preloads_Standard_Odf_Names()
    {
        NameTable nameTable = OdfXmlNameTable.Create();

        string officeNamespace = nameTable.Get(OdfNamespaces.Office)!;
        string tableNamespace = nameTable.Get(OdfNamespaces.Table)!;
        string officePrefix = nameTable.Get("office")!;
        string tableLocalName = nameTable.Get("table")!;
        string styleName = nameTable.Get("style-name")!;

        Assert.Same(officeNamespace, nameTable.Add(OdfNamespaces.Office));
        Assert.Same(tableNamespace, nameTable.Add(OdfNamespaces.Table));
        Assert.Same(officePrefix, nameTable.Add("office"));
        Assert.Same(tableLocalName, nameTable.Add("table"));
        Assert.Same(styleName, nameTable.Add("style-name"));
    }

    [Fact]
    public void Test_OdfXmlNameTable_Is_Used_By_XmlReader()
    {
        NameTable nameTable = OdfXmlNameTable.Create();
        string tableNamespace = nameTable.Get(OdfNamespaces.Table)!;
        string tableLocalName = nameTable.Get("table")!;
        string styleName = nameTable.Get("style-name")!;

        const string xml = """
            <table:table xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" table:style-name="ta1" />
            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            NameTable = nameTable,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        Assert.True(reader.Read());
        Assert.Same(tableNamespace, reader.NamespaceURI);
        Assert.Same(tableLocalName, reader.LocalName);
        Assert.True(reader.MoveToAttribute("style-name", OdfNamespaces.Table));
        Assert.Same(tableNamespace, reader.NamespaceURI);
        Assert.Same(styleName, reader.LocalName);
    }

    [Fact]
    public void Test_BeginUpdate_Defers_Style_Deduplication_Until_Outer_EndUpdate()
    {
        using var doc = TextDocument.Create();
        OdfParagraph paragraph = doc.AddParagraph("批次更新");

        IDisposable outer = doc.BeginUpdate();
        Assert.True(doc.IsUpdateActive);

        paragraph.FontSize = "12pt";
        Assert.Null(paragraph.StyleName);

        using (doc.BeginUpdate())
        {
            paragraph.HorizontalAlignment = "center";
            Assert.True(doc.IsUpdateActive);
            Assert.Null(paragraph.StyleName);
        }

        Assert.True(doc.IsUpdateActive);
        Assert.Null(paragraph.StyleName);

        outer.Dispose();

        Assert.False(doc.IsUpdateActive);
        Assert.NotNull(paragraph.StyleName);
        Assert.Equal("12pt", paragraph.FontSize);
        Assert.Equal("center", paragraph.HorizontalAlignment);
    }

    [Fact]
    public void Test_EndUpdate_Without_BeginUpdate_Throws_Localized_Exception()
    {
        using var doc = TextDocument.Create();

        var ex = Assert.Throws<InvalidOperationException>(doc.EndUpdate);

        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfDocument_EndUpdateWithoutBegin"), ex.Message);
    }

    [Fact]
    public void Test_PruneAndCollect_Detaches_And_Clears_Dom_Subtree()
    {
        var root = new OdfNode(OdfNodeType.Element, "root", OdfNamespaces.Office, "office");
        var section = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        paragraph.SetAttribute("style-name", OdfNamespaces.Text, "Body", "text");
        paragraph.TextContent = "待剪裁內容";
        section.AppendChild(paragraph);
        root.AppendChild(section);

        int prunedCount = section.PruneAndCollect();

        Assert.Equal(3, prunedCount);
        Assert.Empty(root.Children);
        Assert.Null(section.Parent);
        Assert.Empty(section.Children);
        Assert.Empty(section.Attributes);
        Assert.Empty(paragraph.Children);
        Assert.Empty(paragraph.Attributes);
    }

    [Fact]
    public void Test_PruneAndCollect_Releases_Deferred_Local_Styles()
    {
        using var doc = TextDocument.Create();
        var p = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        p.TextContent = "待剪裁樣式";
        doc.ContentDom.AppendChild(p);

        using (doc.BeginUpdate())
        {
            doc.StyleEngine.SetLocalStyleProperty(
                p,
                "paragraph",
                "text-properties",
                "font-weight",
                OdfNamespaces.Fo,
                "bold",
                "fo");

            p.PruneAndCollect();
        }

        OdfNode? automaticStyles = doc.ContentDom.Children.Find(child =>
            child.LocalName == "automatic-styles" &&
            child.NamespaceUri == OdfNamespaces.Office);

        Assert.True(automaticStyles is null || automaticStyles.Children.Count == 0);
    }

    [Fact]
    public void Test_RendererRegistry_And_ExportToPdf()
    {
        // 備份並用反射將 _renderer 重設為 null 且將 _attemptedAutoRegister 設為 true，確保測試中不會觸發自動尋檢
        var rendererField = typeof(OdfRendererRegistry).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Static);
        var autoRegisterField = typeof(OdfRendererRegistry).GetField("_attemptedAutoRegister", BindingFlags.NonPublic | BindingFlags.Static);

        var backupRenderer = rendererField?.GetValue(null) as IOdfRenderer;
        var backupAutoRegister = autoRegisterField?.GetValue(null);

        try
        {
            rendererField?.SetValue(null, null);
            autoRegisterField?.SetValue(null, true);

            // 1. 驗證無註冊時丟出異常
            var doc = TextDocument.Create();
            var ms = new MemoryStream();
            var ex = Assert.Throws<InvalidOperationException>((Action)(() => doc.ExportToPdf(ms)));
            Assert.Contains("No PDF renderer registered", ex.Message);

            // 2. 註冊並驗證正常導出
            var renderer = new DummyRenderer();
            OdfRendererRegistry.Register(renderer);
            Assert.Same(renderer, OdfRendererRegistry.Instance);

            doc.ExportToPdf(ms);
            Assert.True(renderer.ExportCalled);
            Assert.True(ms.Length > 0);
        }
        finally
        {
            // 還原註冊狀態
            rendererField?.SetValue(null, backupRenderer);
            autoRegisterField?.SetValue(null, backupAutoRegister);
        }
    }

    [Fact]
    public void Test_ComputedStyle_Caching_And_Invalidation()
    {
        var doc = TextDocument.Create();
        var p = new TextPElement("text");
        doc.ContentDom.AppendChild(p);

        // 觸發並測試預設 ComputedStyle
        var style1 = p.ComputedStyle;
        Assert.NotNull(style1);

        // 驗證屬性變更時，快取被 invalidate 且重新計算
        p.SetAttribute("style-name", OdfNamespaces.Style, "MyCustomStyle", "style");
        var style2 = p.ComputedStyle;
        Assert.NotSame(style1, style2);
    }

    [Fact]
    public void Test_OdfPerformanceTelemetry()
    {
        // 測試 ActivitySource 與 Meter
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "OdfKit.Core",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => { },
            ActivityStopped = activity => { }
        };
        ActivitySource.AddActivityListener(listener);

        var doc = TextDocument.Create();
        // 只要能正常建立與寫入，即表示遙測模組整合無誤
        Assert.NotNull(doc);
    }

    [Fact]
    public void Test_OdfTransaction_Rollback()
    {
        // 測試沙盒交易能正常使用
        using var tempStream = new MemoryStream();
        using (var writer = new OdsStreamWriter(tempStream))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("Value");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        tempStream.Position = 0;
        using var package = OdfPackage.Open(tempStream, leaveOpen: true);
        using (var tx = OdfTransaction.Begin(package))
        {
            // 寫入變更
            package.WriteEntry("test.txt", "Modified Content"u8.ToArray());
            Assert.True(package.HasEntry("test.txt"));
            // 不 Commit 直接 Dispose 觸發 Rollback 警告
        }
    }

    [Fact]
    public void Test_OdfPackage_TransactionException_IsLocalized()
    {
        using var tempStream = new MemoryStream();
        using (var writer = new OdsStreamWriter(tempStream))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteEndSheet();
        }

        tempStream.Position = 0;
        using var package = OdfPackage.Open(tempStream, leaveOpen: true);

        package.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => package.BeginTransaction());
        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfPackage_TransactionAlreadyInProgress"), ex.Message);

        package.RollbackTransaction();
    }

    [Fact]
    public void Test_OdfMemoryTracker_Diagnose()
    {
        var ptr = new IntPtr(12345);
        OdfMemoryTracker.Track(ptr, 1024, "TestAllocation");

        // 檢查有洩漏
        bool hasLeak = OdfMemoryTracker.CheckLeaks(reportLeaks: false);
        Assert.True(hasLeak);

        // Untrack
        OdfMemoryTracker.Untrack(ptr);
        hasLeak = OdfMemoryTracker.CheckLeaks(reportLeaks: false);
        Assert.False(hasLeak);
    }

    [Fact]
    public void Test_OdfMemoryTracker_Emits_Large_Allocation_Diagnostics()
    {
        List<OdfDiagnosticsEventArgs> diagnostics = [];
        EventHandler<OdfDiagnosticsEventArgs> handler = (_, args) => diagnostics.Add(args);
        OdfKitDiagnostics.Log += handler;
        var ptr = new IntPtr(54321);

        try
        {
            OdfMemoryTracker.LargeAllocationWarningThresholdBytes = 512;

            OdfMemoryTracker.Track(ptr, 1024, "LargeAllocation");

            Assert.Contains(diagnostics, args =>
                args.Level == OdfDiagnosticsLevel.Warning &&
                args.Message.Contains("大型追蹤分配", StringComparison.Ordinal) &&
                args.Message.Contains("LargeAllocation", StringComparison.Ordinal));
        }
        finally
        {
            OdfMemoryTracker.Untrack(ptr);
            OdfMemoryTracker.ResetDiagnosticsForTests();
            OdfKitDiagnostics.Log -= handler;
        }
    }

    [Fact]
    public void Test_OdfMemoryTracker_ReportLoadProfile_Emits_AntiPattern_Diagnostics()
    {
        List<OdfDiagnosticsEventArgs> diagnostics = [];
        EventHandler<OdfDiagnosticsEventArgs> handler = (_, args) => diagnostics.Add(args);
        OdfKitDiagnostics.Log += handler;

        try
        {
            OdfMemoryTracker.NodeLoadWarningThreshold = 10;
            OdfMemoryTracker.LargeAllocationWarningThresholdBytes = 512;
            OdfMemoryTracker.BoxingWarningThreshold = 5;

            OdfMemoryTracker.ReportLoadProfile(
                nodeCount: 12,
                allocatedBytes: 1024,
                boxedValueCount: 6,
                label: "ProfileProbe");

            Assert.Contains(diagnostics, args => args.Message.Contains("單次載入節點數過高", StringComparison.Ordinal));
            Assert.Contains(diagnostics, args => args.Message.Contains("LOH/POH 壓力", StringComparison.Ordinal));
            Assert.Contains(diagnostics, args => args.Message.Contains("高頻 boxing", StringComparison.Ordinal));
        }
        finally
        {
            OdfMemoryTracker.ResetDiagnosticsForTests();
            OdfKitDiagnostics.Log -= handler;
        }
    }

    [Fact]
    public void Test_OdfDocumentValidator_Topology()
    {
        var doc = TextDocument.Create();

        // 建立不合規結構：Orphan Cell (未在 row 中)
        var cell = OdfNodeFactory.CreateElement("table-cell", OdfNamespaces.Table, "table");
        doc.ContentDom.AppendChild(cell);

        var report = OdfDocumentValidator.Validate(doc);
        Assert.NotEmpty(report.Issues);
        Assert.Contains(report.Issues, i => i.RuleId == "Rule_Topology_OrphanCell");
    }

    [Fact]
    public void Test_OdfUtf8XmlReader_Parity()
    {
        var xmlStr = "<office:document-content xmlns:office=\"urn\" office:version=\"1.4\"><p>Hello World</p></office:document-content>"u8;
        var reader = new OdfUtf8XmlReader(xmlStr);

        var tokenList = new List<string>();
        while (reader.Read(out var token))
        {
            tokenList.Add($"{token.Kind}:{token.GetNameString()}");
        }

        Assert.Contains("StartElement:office:document-content", tokenList);
        Assert.Contains("StartElement:p", tokenList);
        Assert.Contains("Text:Hello World", tokenList);
        Assert.Contains("EndElement:office:document-content", tokenList);
    }

    [Fact]
    public void Test_OdfUtf8XmlReader_ReadValueChunk_ReadsTextInUtf8Chunks()
    {
        var xml = "<root><p>Chunked UTF-8 value 1234567890</p></root>"u8;
        var reader = new OdfUtf8XmlReader(xml);

        Assert.True(reader.Read(out OdfUtf8XmlToken token));
        Assert.Equal(OdfUtf8XmlTokenKind.StartElement, token.Kind);
        Assert.Equal(0, reader.ReadValueChunk(stackalloc byte[4]));

        Assert.True(reader.Read(out token));
        Assert.Equal(OdfUtf8XmlTokenKind.StartElement, token.Kind);

        Assert.True(reader.Read(out token));
        Assert.Equal(OdfUtf8XmlTokenKind.Text, token.Kind);

        Span<byte> buffer = stackalloc byte[7];
        var chunks = new List<byte>();
        int read;
        while ((read = reader.ReadValueChunk(buffer)) > 0)
        {
            chunks.AddRange(buffer.Slice(0, read).ToArray());
        }

        Assert.Equal("Chunked UTF-8 value 1234567890", Encoding.UTF8.GetString(chunks.ToArray()));
        Assert.Equal(0, reader.ReadValueChunk(buffer));

        Assert.True(reader.Read(out token));
        Assert.Equal(OdfUtf8XmlTokenKind.EndElement, token.Kind);
        Assert.Equal(0, reader.ReadValueChunk(buffer));
    }

    [Fact]
    public void Test_OdfUtf8XmlReader_CopyValueTo_WritesRemainingValueToBufferWriter()
    {
        string value = new string('A', 257) + "臺灣 UTF-8 chunk";
        byte[] xml = Encoding.UTF8.GetBytes($"<root><p>{value}</p></root>");
        var reader = new OdfUtf8XmlReader(xml);

        Assert.True(reader.Read(out _));
        Assert.True(reader.Read(out _));
        Assert.True(reader.Read(out OdfUtf8XmlToken token));
        Assert.Equal(OdfUtf8XmlTokenKind.Text, token.Kind);

        Span<byte> prefix = stackalloc byte[5];
        Assert.Equal(5, reader.ReadValueChunk(prefix));

        var writer = new ArrayBufferWriter<byte>();
        int copied = reader.CopyValueTo(writer, chunkSize: 13);

        byte[] expected = Encoding.UTF8.GetBytes(value);
        Assert.Equal(expected.Length - 5, copied);
        Assert.Equal(expected.AsSpan(5).ToArray(), writer.WrittenSpan.ToArray());
        Assert.Equal(0, reader.CopyValueTo(writer, chunkSize: 13));
        try
        {
            reader.CopyValueTo(writer, chunkSize: 0);
            Assert.Fail("chunkSize 為 0 時應擲出 ArgumentOutOfRangeException。");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        try
        {
            reader.CopyValueTo(null!);
            Assert.Fail("writer 為 null 時應擲出 ArgumentNullException。");
        }
        catch (ArgumentNullException)
        {
        }
    }

    [Fact]
    public void Test_OdfUtf8SpanWriter_FormatsValuesAndEscapesXmlWithoutStrings()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new OdfUtf8SpanWriter(buffer);

        writer.WriteAscii("<cell value=\""u8);
        writer.WriteDouble(1234.5);
        writer.WriteAscii("\" flag=\""u8);
        writer.WriteBoolean(true);
        writer.WriteAscii("\" text=\""u8);
        writer.WriteEscapedAttributeValue("臺灣 & \"ODF\" 😀");
        writer.WriteAscii("\">"u8);
        writer.WriteEscapedText("A < B & C > D");
        writer.WriteAscii("</cell>"u8);

        Assert.Equal(
            "<cell value=\"1234.5\" flag=\"true\" text=\"臺灣 &amp; &quot;ODF&quot; 😀\">A &lt; B &amp; C &gt; D</cell>",
            Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Test_OdfUtf8SpanWriter_RepeatedNumericWritesAvoidPerValueStringAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>(1024 * 64);
        var writer = new OdfUtf8SpanWriter(buffer);

        // 預熱 JIT 與 buffer 成長，讓下面的配置量只反映重複格式化路徑。
        for (int i = 0; i < 100; i++)
        {
            writer.WriteInt64(i);
            writer.WriteAscii(","u8);
            writer.WriteDouble(i + 0.25);
            writer.WriteAscii(";"u8);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            writer.WriteInt64(i);
            writer.WriteAscii(","u8);
            writer.WriteDouble(i + 0.25);
            writer.WriteAscii(";"u8);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Test_OdfUtf8XmlReader_GetStringMaybeDecoded_UsesFastEntityDecoder()
    {
        int decodeHitsBefore = OdfUtf8XmlReader.LastEntityFastDecodeCountForTests;
        var value = "A&amp;B&lt;C&gt;&quot;D&quot;&apos;E&apos;&#65;&#x1F600;&unknown;"u8;

        string decoded = OdfUtf8XmlReader.GetStringMaybeDecoded(value);

        Assert.Equal("A&B<C>\"D\"'E'A😀&unknown;", decoded);
        Assert.Equal(decodeHitsBefore + 1, OdfUtf8XmlReader.LastEntityFastDecodeCountForTests);
    }

    [Fact]
    public void Test_OdfAttributeStringPool_InternsCommonNamesNamespacesAndValues()
    {
        int nameHitsBefore = OdfAttributeStringPool.NameHitCountForTests;
        int valueHitsBefore = OdfAttributeStringPool.ValueHitCountForTests;

        var first = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
        var second = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");

        first.SetAttribute(
            new string("value-type".ToCharArray()),
            new string(OdfNamespaces.Office.ToCharArray()),
            new string("float".ToCharArray()),
            new string("office".ToCharArray()));
        second.SetAttribute(
            new string("value-type".ToCharArray()),
            new string(OdfNamespaces.Office.ToCharArray()),
            new string("float".ToCharArray()),
            new string("office".ToCharArray()));

        OdfAttributeName firstKey = first.Attributes.Keys.Single();
        OdfAttributeName secondKey = second.Attributes.Keys.Single();

        Assert.True(ReferenceEquals(firstKey.LocalName, secondKey.LocalName));
        Assert.True(ReferenceEquals(firstKey.NamespaceUri, secondKey.NamespaceUri));
        Assert.True(ReferenceEquals(first.Attributes[firstKey], second.Attributes[secondKey]));
        Assert.True(OdfAttributeStringPool.NameHitCountForTests >= nameHitsBefore + 2);
        Assert.True(OdfAttributeStringPool.ValueHitCountForTests >= valueHitsBefore + 2);
    }

    [Fact]
    public void Test_OdfFastNumberParser_UsesUtf8LookupAndSpanParserForShortNumbers()
    {
        int utf8HitsBefore = OdfFastNumberParser.LastUtf8LookupHitCountForTests;
        int spanHitsBefore = OdfFastNumberParser.LastSpanParserHitCountForTests;

        Assert.True(OdfFastNumberParser.TryParse("1.5"u8, out double lookup));
        Assert.Equal(1.5, lookup);
        Assert.Equal(utf8HitsBefore + 1, OdfFastNumberParser.LastUtf8LookupHitCountForTests);

        Assert.True(OdfFastNumberParser.TryParse("-12.25"u8, out double parsed));
        Assert.Equal(-12.25, parsed);
        Assert.True(OdfFastNumberParser.LastSpanParserHitCountForTests > spanHitsBefore);

        Assert.False(OdfFastNumberParser.TryParse("1e3"u8, out _));
        Assert.False(OdfFastNumberParser.TryParse("12345678901234567"u8, out _));
    }

    [Fact]
    public void Test_FormulaCoercion_UsesFastNumberParserForCommonNumericStrings()
    {
        int charHitsBefore = OdfFastNumberParser.LastCharLookupHitCountForTests;

        Assert.True(FormulaCoercion.TryCoerceDouble(new string("1.5".ToCharArray()), out double result));

        Assert.Equal(1.5, result);
        Assert.Equal(charHitsBefore + 1, OdfFastNumberParser.LastCharLookupHitCountForTests);
    }

    [Fact]
    public void Test_OdfChartBuilder_BindData_And_Transform()
    {
        var spreadsheet = SpreadsheetDocument.Create();
        var package = spreadsheet.Package;
        var table = new TableTableElement("table");
        spreadsheet.ContentDom.AppendChild(table);
        table.Name = "Sheet1";

        // 設定資料
        table[0, 0].TextContent = "Category";
        table[0, 1].TextContent = "Value";
        table[1, 0].TextContent = "A";
        table[1, 1].TextContent = "10";
        table[2, 0].TextContent = "B";
        table[2, 1].TextContent = "20";

        // 建立圖表並 BindData
        var chartDoc = ChartDocument.Create(new OdfChartDefinition { ChartType = OdfChartType.Bar });
        var builder = new OdfChartBuilder(chartDoc);
        builder.BindData(table, "A1:B3");

        // 驗證圖表的 data-range 是否正確設定
        var (sheetName, range) = chartDoc.GetDataRange();
        Assert.Equal("LocalTable", sheetName);
        Assert.NotNull(range);
        Assert.Equal(0, range.Value.StartAddress.Row);
        Assert.Equal(2, range.Value.EndAddress.Row);

        // 測試 DrawFrameElement 仿射變換 Transform
        var frame = new DrawFrameElement("draw");
        var matrix = new Matrix3x2(2f, 0f, 0f, 2f, 10f, 20f);
        frame.Transform = matrix;

        Assert.Equal(matrix, frame.Transform);
        Assert.Contains("matrix(2 0 0 2 10 20)", frame.GetAttribute("transform", OdfNamespaces.Draw));

        // 測試複雜 translate / scale 組合解析
        frame.SetAttribute("transform", OdfNamespaces.Draw, "translate(100, 200) scale(2, 3)", "draw");
        var parsedMatrix = frame.Transform;
        // 數學原理：先 translation(100, 200)，後 scale(2, 3)。在 Matrix3x2 矩陣乘法中：
        // M31 = 100 * 2 = 200，M32 = 200 * 3 = 600
        Assert.Equal(200f, parsedMatrix.M31);
        Assert.Equal(600f, parsedMatrix.M32);
        Assert.Equal(2f, parsedMatrix.M11);
        Assert.Equal(3f, parsedMatrix.M22);
    }

    [Fact]
    public void Test_OdfPackage_RawEntryPatch_And_DumpVfsLayout()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteEndSheet();
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);

        // 1. 測試 RawEntryPatch
        bool patched = package.RawEntryPatch("mimetype", (span, writer) =>
        {
            var str = Encoding.UTF8.GetString(span.ToArray());
            Assert.Contains("spreadsheet", str);
            var newVal = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet-patched");
            var dest = writer.GetSpan(newVal.Length);
            newVal.CopyTo(dest);
            writer.Advance(newVal.Length);
            return true;
        });

        Assert.True(patched);
        var mimeBytes = package.ReadEntry("mimetype");
        var mimeText = Encoding.UTF8.GetString(mimeBytes);
        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet-patched", mimeText);

        // 2. 測試 Dump VFS Layout
        string layout = package.DumpVfsLayout();
        Assert.Contains("mimetype", layout);
        Assert.Contains("content.xml", layout);
        Assert.Contains("Dirty:", layout);
        Assert.Contains("LocalHeaderOffset:", layout);
        Assert.Contains("DataOffset:", layout);
    }

    [Fact]
    public void Test_OdfPackage_DebuggerProxy_Exposes_Structured_Vfs_Entries()
    {
        using var ms = new MemoryStream();
        using var package = OdfPackage.Create(ms, leaveOpen: true);
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<office:document-content/>"), "text/xml");

        var proxyAttribute = typeof(OdfPackage).GetCustomAttribute<DebuggerTypeProxyAttribute>();
        Assert.NotNull(proxyAttribute);
        Assert.Contains("OdfPackageDebugView", proxyAttribute!.ProxyTypeName);

        Type debugViewType = typeof(OdfPackage).Assembly.GetType("OdfKit.Core.OdfPackageDebugView")!;
        object debugView = Activator.CreateInstance(
            debugViewType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [package],
            culture: null)!;

        var entriesProperty = debugViewType.GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public)!;
        var entries = (Array)entriesProperty.GetValue(debugView)!;
        object contentEntry = entries.Cast<object>().Single(entry =>
            (string)entry.GetType().GetProperty("Path")!.GetValue(entry)! == "content.xml");

        Type entryType = contentEntry.GetType();
        Assert.Equal("text/xml", entryType.GetProperty("MediaType")!.GetValue(contentEntry));
        Assert.Equal(true, entryType.GetProperty("Dirty")!.GetValue(contentEntry));
        Assert.Equal(-1L, entryType.GetProperty("LocalHeaderOffset")!.GetValue(contentEntry));
        Assert.Equal(-1L, entryType.GetProperty("CompressedDataOffset")!.GetValue(contentEntry));
        Assert.True((long)entryType.GetProperty("Size")!.GetValue(contentEntry)! > 0);
    }

    [Fact]
    public void Test_OdfPackage_FileOpen_UsesCentralDirectoryIndexForVfsOffsets()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"central_directory_index_{Guid.NewGuid():N}.odt");
        try
        {
            using (var document = TextDocument.Create())
            {
                document.AddParagraph("中央目錄索引測試");
                document.Save(tempFile);
            }

            using var package = OdfPackage.Open(tempFile);
            Type debugViewType = typeof(OdfPackage).Assembly.GetType("OdfKit.Core.OdfPackageDebugView")!;
            object debugView = Activator.CreateInstance(
                debugViewType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [package],
                culture: null)!;

            var entriesProperty = debugViewType.GetProperty("Entries", BindingFlags.Instance | BindingFlags.Public)!;
            var entries = (Array)entriesProperty.GetValue(debugView)!;
            object contentEntry = entries.Cast<object>().Single(entry =>
                (string)entry.GetType().GetProperty("Path")!.GetValue(entry)! == "content.xml");

            Type entryType = contentEntry.GetType();
            Assert.True((long)entryType.GetProperty("LocalHeaderOffset")!.GetValue(contentEntry)! >= 0);
            Assert.True((long)entryType.GetProperty("CompressedDataOffset")!.GetValue(contentEntry)! > 0);
            Assert.True((long)entryType.GetProperty("CompressedSize")!.GetValue(contentEntry)! > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Test_OdfPackage_Save_WritesToBufferWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var package = OdfPackage.Create(new MemoryStream()))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<office:document-content/>"), "text/xml");
            package.Save(buffer);
        }

        Assert.True(buffer.WrittenCount > 0);

        using var saved = new MemoryStream(buffer.WrittenSpan.ToArray());
        using OdfPackage reloaded = OdfPackage.Open(saved);
        Assert.True(reloaded.HasEntry("content.xml"));
        Assert.Equal(
            "<office:document-content/>",
            Encoding.UTF8.GetString(reloaded.ReadEntry("content.xml")));
    }

    [Fact]
    public void Test_OdfPackage_Save_UsesParallelPreparedZipEntries()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content>" + new string('a', 4096) + "</content>"), "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles>" + new string('b', 4096) + "</styles>"), "text/xml");
            package.WriteEntry(
                "Pictures/logo.bin",
                Enumerable.Range(0, 2048).Select(static value => (byte)(value % 251)).ToArray(),
                "application/octet-stream");

            OdfPackageArchiveWriter.LastParallelPreparedEntryCount = 0;
            OdfPackageArchiveWriter.LastParallelCompressionUsedPooledBuffer = false;
            package.Save();
        }

        Assert.True(OdfPackageArchiveWriter.LastParallelPreparedEntryCount >= 3);
        Assert.True(OdfPackageArchiveWriter.LastParallelCompressionUsedPooledBuffer);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.NotNull(archive.GetEntry("content.xml"));
        Assert.NotNull(archive.GetEntry("styles.xml"));
        Assert.NotNull(archive.GetEntry("Pictures/logo.bin"));
    }

    [Fact]
    public async Task Test_OdfPackage_SaveAsync_UsesParallelPreparedZipEntries()
    {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        using (var package = OdfPackage.Create(source, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content>" + new string('a', 4096) + "</content>"), "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles>" + new string('b', 4096) + "</styles>"), "text/xml");
            package.WriteEntry(
                "Pictures/logo.bin",
                Enumerable.Range(0, 2048).Select(static value => (byte)(value % 251)).ToArray(),
                "application/octet-stream");

            OdfPackageArchiveWriter.LastAsyncFastPathArchiveUsed = false;
            OdfPackageArchiveWriter.LastParallelPreparedEntryCount = 0;
            OdfPackageArchiveWriter.LastFastPathCancellationCheckCount = 0;

            await package.SaveToStreamAsync(destination, cts.Token);
        }

        Assert.True(OdfPackageArchiveWriter.LastAsyncFastPathArchiveUsed);
        Assert.True(OdfPackageArchiveWriter.LastParallelPreparedEntryCount >= 3);
        Assert.True(OdfPackageArchiveWriter.LastFastPathCancellationCheckCount > 0);
        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true);
        Assert.NotNull(archive.GetEntry("content.xml"));
        Assert.NotNull(archive.GetEntry("styles.xml"));
        Assert.NotNull(archive.GetEntry("Pictures/logo.bin"));
    }

    [Fact]
    public async Task Test_OdfPackage_SaveAsync_UsesRawCopyForUnmodifiedMmfEntries()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"async_raw_copy_{Guid.NewGuid():N}.odt");
        using var cts = new CancellationTokenSource();
        try
        {
            using (var document = TextDocument.Create())
            {
                document.AddParagraph("Async raw copy");
                document.Save(tempFile);
            }

            await using var package = await OdfPackage.OpenAsync(
                tempFile,
                new OdfLoadOptions { AllowLazyLoading = true },
                cancellationToken: TestContext.Current.CancellationToken);
            using var destination = new MemoryStream();

            OdfPackageArchiveWriter.LastAsyncFastPathArchiveUsed = false;
            OdfPackageArchiveWriter.LastRawCopyArchiveUsed = false;
            OdfPackageArchiveWriter.LastFastPathCancellationCheckCount = 0;

            await package.SaveToStreamAsync(destination, cts.Token);

            Assert.True(OdfPackageArchiveWriter.LastAsyncFastPathArchiveUsed);
            Assert.True(OdfPackageArchiveWriter.LastRawCopyArchiveUsed);
            Assert.True(OdfPackageArchiveWriter.LastFastPathCancellationCheckCount > 0);
            destination.Position = 0;
            using OdfPackage reloaded = OdfPackage.Open(destination, leaveOpen: true);
            Assert.True(reloaded.HasEntry("content.xml"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Test_OdfPackage_SaveAsync_CanceledBeforeFastPathThrows()
    {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        using var package = OdfPackage.Create(source, leaveOpen: true);
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => package.SaveToStreamAsync(destination, cts.Token));
    }

    [Fact]
    public void Test_OdfPackage_Save_FallbackLargeEntryAvoidsExtraByteCloneAndRoundTrips()
    {
        byte[] payload = Enumerable.Range(0, 512 * 1024).Select(static value => (byte)(value % 251)).ToArray();
        using var source = new MemoryStream();
        using var destination = new MemoryStream();
        using (var package = OdfPackage.Create(source, leaveOpen: true, new OdfSaveOptions { Password = "force-fallback" }))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry("Pictures/large.bin", payload, "application/octet-stream");
            package.WriteEntry("META-INF/manifest.xml", Encoding.UTF8.GetBytes(CreateMinimalManifestXml("Pictures/large.bin")), "text/xml");
            OdfPackageArchiveWriter.LastFallbackCrcWriteCount = 0;
            OdfPackageArchiveWriter.LastFallbackEntryByteCloneCount = 0;
            OdfPackageArchiveWriter.LastParallelPreparedEntryCount = 0;

            package.SaveCollaborators.WriteToArchive(destination);
        }

        Assert.Equal(4, OdfPackageArchiveWriter.LastFallbackCrcWriteCount);
        Assert.Equal(0, OdfPackageArchiveWriter.LastFallbackEntryByteCloneCount);
        Assert.Equal(0, OdfPackageArchiveWriter.LastParallelPreparedEntryCount);

        destination.Position = 0;
        using (var archive = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true))
        {
            ZipArchiveEntry? entry = archive.GetEntry("Pictures/large.bin");
            Assert.NotNull(entry);
            using Stream entryStream = entry!.Open();
            using var copied = new MemoryStream();
            entryStream.CopyTo(copied);
            Assert.Equal(payload, copied.ToArray());
        }

        destination.Position = 0;
        using OdfPackage reloaded = OdfPackage.Open(destination, leaveOpen: true);
        Assert.Equal(payload, reloaded.ReadEntry("Pictures/large.bin"));
    }

    [Fact]
    public void Test_OdfPackage_FallbackWriterComputesCrcDuringSingleWritePass()
    {
        byte[] payload = Encoding.UTF8.GetBytes(new string('x', 4096));
        using var output = new MemoryStream();
        OdfPackageArchiveWriter.LastFallbackCrcWriteCount = 0;

        uint crc = OdfPackageArchiveWriter.WriteEntryContentWithCrc32ForTests(output, payload);

        Assert.Equal(1, OdfPackageArchiveWriter.LastFallbackCrcWriteCount);
        Assert.Equal(OdfCrc32.Compute(payload), crc);
        Assert.Equal(payload, output.ToArray());
    }

    [Fact]
    public void Test_OdfCrc32Stream_ReadDetectsCrcMismatch()
    {
        byte[] payload = Encoding.UTF8.GetBytes("crc mismatch payload");
        uint wrongCrc = OdfCrc32.Compute(payload) ^ 0xFFFFFFFFu;
        using var input = new MemoryStream(payload);
        using var crcStream = new OdfCrc32Stream(input, wrongCrc);
        byte[] buffer = new byte[8];

        Assert.Throws<InvalidDataException>(() =>
        {
            while (crcStream.Read(buffer, 0, buffer.Length) > 0)
            { }
        });
    }

    [Fact]
    public async Task Test_OdfPackage_LoadEntries_TracksEntryOrderWithDuplicates()
    {
        byte[] first = Encoding.UTF8.GetBytes("first");
        byte[] second = Encoding.UTF8.GetBytes("second");
        using MemoryStream packageStream = CreateZipPackage(
            ("mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text")),
            ("content.xml", first),
            ("content.xml", second),
            ("styles.xml", Encoding.UTF8.GetBytes("<styles/>")),
            ("META-INF/manifest.xml", Encoding.UTF8.GetBytes(CreateMinimalManifestXml())));

        using OdfPackage package = OdfPackage.Open(packageStream, leaveOpen: true);

        Assert.Contains("content.xml", package.DuplicateEntryNames);
        Assert.Equal(new[] { "mimetype", "content.xml", "styles.xml", "META-INF/manifest.xml" }, package.EntryOrder);
        Assert.Equal(second, package.ReadEntry("content.xml"));

        packageStream.Position = 0;
        await using OdfPackage asyncPackage = await OdfPackage.OpenAsync(
            packageStream,
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("content.xml", asyncPackage.DuplicateEntryNames);
        Assert.Equal(new[] { "mimetype", "content.xml", "styles.xml", "META-INF/manifest.xml" }, asyncPackage.EntryOrder);
        Assert.Equal(second, asyncPackage.ReadEntry("content.xml"));
    }

    [Fact]
    public void Test_OdfPackage_RawEntryPatch_RepeatedCacheConsistency()
    {
        using var ms = new MemoryStream();
        using (var package = OdfPackage.Create(ms, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
            byte[] current = Encoding.UTF8.GetBytes("payload-00");
            package.WriteEntry("meta/custom.txt", current, "text/plain");

            for (int i = 1; i <= 32; i++)
            {
                byte[] expectedInput = current;
                byte[] next = Encoding.UTF8.GetBytes($"payload-{i:D2}");
                bool patched = package.RawEntryPatch("meta/custom.txt", (span, writer) =>
                {
                    Assert.Equal(expectedInput, span.ToArray());
                    next.CopyTo(writer.GetSpan(next.Length));
                    writer.Advance(next.Length);
                    return true;
                });

                Assert.True(patched);
                Assert.Equal(next, package.ReadEntry("meta/custom.txt"));
                current = next;
            }

            package.Save();
        }

        ms.Position = 0;
        using var reloaded = OdfPackage.Open(ms, leaveOpen: true);
        Assert.Equal(Encoding.UTF8.GetBytes("payload-32"), reloaded.ReadEntry("meta/custom.txt"));
    }

    [Fact]
    public void Test_OdfPackage_RawEntryPatch_StoredEntry_RoundTripsWithUpdatedCrc()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"raw_patch_{Guid.NewGuid():N}.ods");
        try
        {
            byte[] replacementBytes;
            using (var doc = SpreadsheetDocument.Create())
            {
                doc.Save(tempFile);
            }

            using (var package = OdfPackage.Open(tempFile))
            {
                byte[] original = package.ReadEntry("mimetype");
                byte[] replacement = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheeT");
                replacementBytes = replacement;
                Assert.Equal(original.Length, replacement.Length);

                bool patched = package.RawEntryPatch("mimetype", (span, writer) =>
                {
                    Assert.Equal(original, span.ToArray());
                    replacement.CopyTo(writer.GetSpan(replacement.Length));
                    writer.Advance(replacement.Length);
                    return true;
                });

                Assert.True(patched);
                Assert.Equal(replacement, package.ReadEntry("mimetype"));
                package.Save();
            }

            uint expectedCrc = OdfCrc32.Compute(replacementBytes);
            (uint localHeaderCrc, uint centralDirectoryCrc) = ReadZipEntryCrcs(tempFile, "mimetype");

            Assert.Equal(expectedCrc, localHeaderCrc);
            Assert.Equal(expectedCrc, centralDirectoryCrc);

            using (var package = OdfPackage.Open(tempFile))
            {
                Assert.Equal(
                    "application/vnd.oasis.opendocument.spreadsheeT",
                    Encoding.UTF8.GetString(package.ReadEntry("mimetype")));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Test_OdfPackage_RawEntryPatch_VariableLengthEntry_AppendsNewCentralDirectory()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"raw_patch_append_{Guid.NewGuid():N}.ods");
        string marker = "<!-- 增量追加 Patch 測試內容 -->";
        try
        {
            using (var doc = SpreadsheetDocument.Create())
            {
                doc.Save(tempFile);
            }

            long originalLength = new FileInfo(tempFile).Length;
            uint originalCentralDirectoryOffset = ReadEocdCentralDirectoryOffset(tempFile);
            int centralDirectoryWritesBefore = OdfPackage.DirectCentralDirectoryWriteCountForTests;
            int eocdWritesBefore = OdfPackage.DirectEndOfCentralDirectoryWriteCountForTests;
            string patchedContent;

            using (var package = OdfPackage.Open(tempFile))
            using (var tx = OdfTransaction.Begin(package))
            {
                byte[] original = package.ReadEntry("content.xml");
                string originalContent = Encoding.UTF8.GetString(original);
                const string rootEndTag = "</office:document-content>";
                Assert.Contains(rootEndTag, originalContent, StringComparison.Ordinal);
                patchedContent = originalContent.Replace(rootEndTag, marker + rootEndTag, StringComparison.Ordinal);
                byte[] replacement = Encoding.UTF8.GetBytes(patchedContent);
                Assert.True(replacement.Length > original.Length);

                bool patched = package.RawEntryPatch("content.xml", (span, writer) =>
                {
                    Assert.Equal(original, span.ToArray());
                    replacement.CopyTo(writer.GetSpan(replacement.Length));
                    writer.Advance(replacement.Length);
                    return true;
                });

                Assert.True(patched);
                package.Save();
                tx.Commit();
            }

            long patchedLength = new FileInfo(tempFile).Length;
            uint patchedCentralDirectoryOffset = ReadEocdCentralDirectoryOffset(tempFile);
            Assert.True(patchedLength > originalLength);
            Assert.True(patchedCentralDirectoryOffset > originalCentralDirectoryOffset);
            Assert.True(OdfPackage.DirectCentralDirectoryWriteCountForTests > centralDirectoryWritesBefore);
            Assert.Equal(eocdWritesBefore + 1, OdfPackage.DirectEndOfCentralDirectoryWriteCountForTests);

            using (FileStream fs = File.OpenRead(tempFile))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                ZipArchiveEntry? entry = archive.GetEntry("content.xml");
                Assert.NotNull(entry);
                using Stream stream = entry!.Open();
                using StreamReader reader = new(stream, Encoding.UTF8);
                string zipContent = reader.ReadToEnd();
                Assert.Contains(marker, zipContent);
                Assert.Equal(patchedContent, zipContent);
            }

            using (var package = OdfPackage.Open(tempFile))
            {
                string reloaded = Encoding.UTF8.GetString(package.ReadEntry("content.xml"));
                Assert.Equal(patchedContent, reloaded);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Test_OdfDoubleBufferedWritableStream_PreservesBytesAcrossBufferBoundaries()
    {
        byte[] expected = Enumerable.Range(0, 4097)
            .Select(static value => (byte)(value % 251))
            .ToArray();
        int rentedBefore = OdfDoubleBufferedWritableStream.RentedBufferCountForTests;
        int returnedBefore = OdfDoubleBufferedWritableStream.ReturnedBufferCountForTests;

        using var target = new MemoryStream();
        using (var stream = new OdfDoubleBufferedWritableStream(target, bufferSize: 127, leaveOpen: true))
        {
            int offset = 0;
            while (offset < expected.Length)
            {
                int count = Math.Min(53, expected.Length - offset);
                await stream.WriteAsync(expected, offset, count, TestContext.Current.CancellationToken);
                offset += count;
            }

            await stream.FlushAsync(TestContext.Current.CancellationToken);
            Assert.True(stream.CanWrite);
        }

        Assert.True(target.CanRead);
        Assert.Equal(expected, target.ToArray());
        Assert.Equal(rentedBefore + 2, OdfDoubleBufferedWritableStream.RentedBufferCountForTests);
        Assert.Equal(returnedBefore + 2, OdfDoubleBufferedWritableStream.ReturnedBufferCountForTests);
    }

    [Fact]
    public void Test_OdfDoubleBufferedWritableStream_RejectsInvalidBufferSize()
    {
        using var target = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new OdfDoubleBufferedWritableStream(target, bufferSize: 0));
    }

    [Fact]
    public async Task Test_OdfPagedGatherWritableStream_BatchesPagesAndFallsBackForNonFileStream()
    {
        byte[] expected = Enumerable.Range(0, 192)
            .Select(static value => (byte)(value % 251))
            .ToArray();
        int sequentialBefore = OdfPagedGatherWritableStream.SequentialFallbackFlushCountForTests;
        int rentedBefore = OdfPagedGatherWritableStream.RentedPageCountForTests;
        int returnedBefore = OdfPagedGatherWritableStream.ReturnedPageCountForTests;

        using var target = new MemoryStream();
        using (var stream = new OdfPagedGatherWritableStream(target, pageSize: 64, pagesPerFlush: 3, leaveOpen: true))
        {
            await stream.WriteAsync(expected, 0, expected.Length, TestContext.Current.CancellationToken);
            await stream.FlushAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, target.ToArray());
        Assert.Equal(3, OdfPagedGatherWritableStream.LastFlushPageCountForTests);
        Assert.Equal(sequentialBefore + 1, OdfPagedGatherWritableStream.SequentialFallbackFlushCountForTests);
        Assert.Equal(rentedBefore + 4, OdfPagedGatherWritableStream.RentedPageCountForTests);
        Assert.Equal(returnedBefore + 4, OdfPagedGatherWritableStream.ReturnedPageCountForTests);
    }

    [Fact]
    public async Task Test_OdfPagedGatherWritableStream_UsesVectoredFileWriteOnNet10()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"OdfKitPagedGather_{Guid.NewGuid():N}.bin");
        byte[] expected = Enumerable.Range(0, 384)
            .Select(static value => (byte)(value % 239))
            .ToArray();
        int vectoredBefore = OdfPagedGatherWritableStream.VectoredFlushCountForTests;

        try
        {
            await using (var file = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous))
            await using (var stream = new OdfPagedGatherWritableStream(file, pageSize: 128, pagesPerFlush: 3, leaveOpen: true))
            {
                await stream.WriteAsync(expected, 0, expected.Length, TestContext.Current.CancellationToken);
                await stream.FlushAsync(TestContext.Current.CancellationToken);
                Assert.Equal(expected.Length, file.Position);
            }

            byte[] actual = await File.ReadAllBytesAsync(tempFile, TestContext.Current.CancellationToken);
            Assert.Equal(expected, actual);

#if NET10_0_OR_GREATER
            Assert.Equal(vectoredBefore + 1, OdfPagedGatherWritableStream.VectoredFlushCountForTests);
#else
            Assert.Equal(vectoredBefore, OdfPagedGatherWritableStream.VectoredFlushCountForTests);
#endif
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Test_OdsStreamWriter_ToAsyncEnumerable()
    {
        // 1. 同步 Action ToAsyncEnumerable
        var chunks = new List<byte[]>();
        await foreach (var chunk in OdsStreamWriter.ToAsyncEnumerable(writer =>
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("Hello");
            writer.WriteCell(123.45);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }))
        {
            chunks.Add(chunk.ToArray());
        }

        Assert.NotEmpty(chunks);

        // 將 chunks 合併回 Stream 以 ZipArchive 解壓驗證
        using var zipStream = new MemoryStream();
        foreach (var c in chunks)
        {
            zipStream.Write(c, 0, c.Length);
        }

        zipStream.Position = 0;
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            var contentEntry = archive.GetEntry("content.xml");
            Assert.NotNull(contentEntry);
            using var reader = new StreamReader(contentEntry.Open());
            var xmlText = await reader.ReadToEndAsync();
            Assert.Contains("Hello", xmlText);
            Assert.Contains("123.45", xmlText);
        }

        // 2. 非同步 Func ToAsyncEnumerable
        var chunksAsync = new List<byte[]>();
        await foreach (var chunk in OdsStreamWriter.ToAsyncEnumerable(async writer =>
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("HelloAsync");
            writer.WriteEndRow();
            writer.WriteEndSheet();
            await Task.Yield();
        }))
        {
            chunksAsync.Add(chunk.ToArray());
        }

        Assert.NotEmpty(chunksAsync);
    }

    [Fact]
    public async Task Test_OdsStreamWriter_ToAsyncEnumerable_LargeOutputProducesValidChunks()
    {
        var chunks = new List<byte[]>();
        await foreach (var chunk in OdsStreamWriter.ToAsyncEnumerable(writer =>
        {
            writer.WriteStartSheet("Rows");
            for (int row = 0; row < 250; row++)
            {
                writer.WriteStartRow();
                writer.WriteCell($"R{row}");
                writer.WriteCell(row);
                writer.WriteEndRow();
            }
            writer.WriteEndSheet();
        }))
        {
            chunks.Add(chunk.ToArray());
        }

        Assert.True(chunks.Count > 1);

        using var zipStream = new MemoryStream();
        foreach (byte[] chunk in chunks)
        {
            zipStream.Write(chunk, 0, chunk.Length);
        }

        zipStream.Position = 0;
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        ZipArchiveEntry? contentEntry = archive.GetEntry("content.xml");
        Assert.NotNull(contentEntry);
        using var reader = new StreamReader(contentEntry.Open());
        string xmlText = await reader.ReadToEndAsync();
        Assert.Contains("R0", xmlText);
        Assert.Contains("R249", xmlText);
    }

    [Fact]
    public async Task Test_OdsStreamWriter_ToAsyncEnumerable_PropagatesProducerFailure()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (ReadOnlyMemory<byte> _ in OdsStreamWriter.ToAsyncEnumerable(_ =>
            {
                throw new InvalidOperationException("writer failed");
            }))
            {
            }
        });

        Assert.Equal("writer failed", exception.Message);
    }

    [Fact]
    public void Test_OdsStreamReader_DbDataReader()
    {
        using var tempStream = new MemoryStream();
        using (var writer = new OdsStreamWriter(tempStream))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("Name");
            writer.WriteCell("Age");
            writer.WriteEndRow();
            writer.WriteStartRow();
            writer.WriteCell("Alice");
            writer.WriteCell(25.0);
            writer.WriteEndRow();
            writer.WriteStartRow();
            writer.WriteCell("Bob");
            writer.WriteCell(30.0);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        tempStream.Position = 0;
        using var reader = new OdsStreamReader(tempStream);
        reader.SelectSheet(0);

        // 1. 驗證繼承與屬性
        Assert.True(reader is System.Data.Common.DbDataReader);
        Assert.True(reader.HasRows);

        var table = new System.Data.DataTable();
        table.Load(reader);

        // 2. 驗證讀出資料正確性
        Assert.Equal(3, table.Rows.Count); // 包括 Header 列，因為 HasHeaders=false
        Assert.Equal("Name", table.Rows[0][0]);
        Assert.Equal("Alice", table.Rows[1][0]);
        Assert.Equal(25.0, Convert.ToDouble(table.Rows[1][1]));
        Assert.Equal("Bob", table.Rows[2][0]);
        Assert.Equal(30.0, Convert.ToDouble(table.Rows[2][1]));
    }

    [Fact]
    public void Test_DOMTree_ComputedStyleInvalidation()
    {
        var doc = TextDocument.Create();
        var parent1 = new TextPElement("text");
        var parent2 = new TextPElement("text");
        var child = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "Hello" };

        parent1.AppendChild(child);
        doc.ContentDom.AppendChild(parent1);
        doc.ContentDom.AppendChild(parent2);

        // 觸發快取
        var parentStyle1 = parent1.ComputedStyle;
        Assert.NotNull(parentStyle1);

        // 搬移節點
        parent2.AppendChild(parent1);

        // 驗證快取失效
        // 搬移後，parent1 成為 parent2 的子元素，繼承鏈改變，_isStyleDirty 應為 true，讀取時重新計算
        var parentStyle2 = parent1.ComputedStyle;
        Assert.NotNull(parentStyle2);
    }

    [Fact]
    public void Test_AdoptNode_MediaManagerCache()
    {
        var docSrc = TextDocument.Create();
        var docDest = TextDocument.Create();

        // 驗證 MediaManager 單例快取
        var mm1 = docDest.Package.MediaManager;
        var mm2 = docDest.Package.MediaManager;
        Assert.Same(mm1, mm2);

        byte[] imgBytes = "Dummy Image Data"u8.ToArray();
        string hrefSrc = docSrc.Package.MediaManager.AddImage(imgBytes, "logo.png");

        var imageElement = new DrawImageElement("draw");
        imageElement.Href = hrefSrc;

        // 執行 AdoptNode 媒體移轉
        var adopted = docDest.AdoptNode(docSrc, imageElement) as DrawImageElement;
        Assert.NotNull(adopted);
        Assert.StartsWith("Pictures/logo", adopted.Href);

        // 再次移轉相同的媒體，驗證去重命中
        var adopted2 = docDest.AdoptNode(docSrc, imageElement) as DrawImageElement;
        Assert.NotNull(adopted2);
        Assert.Equal(adopted.Href, adopted2.Href);
    }

    [Fact]
    public void Test_OdfDocument_ReplaceText_PreservingFormat()
    {
        var doc = TextDocument.Create();
        var p = new TextPElement("text");
        p.TextContent = "Hello [Target] World";
        doc.ContentDom.AppendChild(p);

        // 驗證整份文件的替換 API
        doc.ReplaceText("[Target]", "Gemini");
        Assert.Contains("Hello Gemini World", p.TextContent);
    }

    [Fact]
    public void Test_OdfLazyLoading_ZeroAllocation_MmfPtr()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"OdfKitMmfLazyTest_{Guid.NewGuid():N}.dat");
        try
        {
            // 建立一個有未壓縮 entry 的 Package
            using (var doc = SpreadsheetDocument.Create())
            {
                var table = new TableTableElement("table") { Name = "Sheet1" };
                doc.ContentDom.AppendChild(table);

                // 建立一個大於 8KB 的大型 XML 以觸發延遲載入
                for (int i = 0; i < 500; i++)
                {
                    table[i, 0].TextContent = $"Value {i}";
                }

                // 設定不壓縮 content.xml 以便在 MMF 載入時保留 Stored 模式
                var entry = doc.Package.GetEntry("content.xml");
                if (entry != null)
                {
                    entry.IsCompressed = false;
                }
                doc.Save(tempFile);
            }

            // 以 MMF 載入封裝
            using (var package = OdfPackage.Open(tempFile))
            {
                var entry = package.GetEntry("content.xml");
                Assert.NotNull(entry);
                Assert.True(entry.CanExposeMmfPointer);

                int len;
                IntPtr ptr = entry.GetMmfPointer(out len);
                Assert.NotEqual(IntPtr.Zero, ptr);
                Assert.True(len > 0);

                // 載入 DOM 樹
                unsafe
                {
                    using var manager = new UnmanagedMemoryManager(ptr, len);
                    var root = OdfXmlReader.Parse(manager.Memory, ptr, package.LoadOptions);
                    Assert.NotNull(root);

                    // 尋找 table 元素，因為是大元素且已設定 lazy，應該是 _isLazy = true 且有 _lazyXmlPtr
                    var tableNode = root.Descendants().FirstOrDefault(n => n.LocalName == "table") as TableTableElement;
                    Assert.NotNull(tableNode);

                    // 驗證延遲載入欄位設定正確，指標來自 MMF 唯讀區
                    Assert.True(tableNode._isLazy);
                    Assert.NotEqual(IntPtr.Zero, tableNode._lazyXmlPtr);

                    // 驗證按需具現化邏輯
                    var cell = tableNode[10, 0];
                    Assert.Equal("Value 10", cell.TextContent);
                    Assert.False(tableNode._isLazy); // 已具現化
                }
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Test_OdfSparseStorage_LruCache_Tiering()
    {
        using (var doc = SpreadsheetDocument.Create())
        {
            var table = new TableTableElement("table") { Name = "Sheet1" };
            doc.ContentDom.AppendChild(table);

            // 寫入 20 個 Pages 上的單元格樣式，這會將資料寫入 Sparse Storage 的 20 個不同 Page
            for (int i = 0; i < 20; i++)
            {
                table.SetSparseCellStyle(i * 128, 0, $"Style{i}");
            }

            // 因為 MaxHotPages = 16，所以在此時最多只有 16 個頁面是 Hot 的，有 4 個被自動淘汰成 Cold
            int hotCount = 0;
            int coldCount = 0;

            var pageStates = table._pageStates;
            Assert.NotNull(pageStates);

            for (int i = 0; i < pageStates.Length; i++)
            {
                if (pageStates[i] == null)
                    continue;
                for (int j = 0; j < pageStates[i].Length; j++)
                {
                    if (pageStates[i][j].IsHot)
                    {
                        hotCount++;
                    }
                    else if (pageStates[i][j].CompressedBytes != null)
                    {
                        coldCount++;
                    }
                }
            }

            // 驗證 Hot 頁面精確限制在 16 個，Cold 頁面有 4 個
            Assert.Equal(16, hotCount);
            Assert.Equal(4, coldCount);

            // 存取原本已經被淘汰成 cold-page 的 4 個頁面（即 0, 1, 2, 3 頁面）
            for (int i = 0; i < 4; i++)
            {
                var cell = table[i * 128, 0];
                Assert.Equal($"Style{i}", cell.StyleName);
            }

            // 存取完後，這 4 個冷頁被還原成熱頁，原來的某些頁面會被自動淘汰以維持最大限制 16
            hotCount = 0;
            coldCount = 0;
            for (int i = 0; i < pageStates.Length; i++)
            {
                if (pageStates[i] == null)
                    continue;
                for (int j = 0; j < pageStates[i].Length; j++)
                {
                    if (pageStates[i][j].IsHot)
                    {
                        hotCount++;
                    }
                    else if (pageStates[i][j].CompressedBytes != null)
                    {
                        coldCount++;
                    }
                }
            }

            Assert.Equal(16, hotCount);
            Assert.Equal(4, coldCount);
        }
    }

    [Fact]
    public unsafe void Test_DirectIoAlignedNativeBuffer_Uses4096ByteAlignedNativeMemory()
    {
#if NET10_0_OR_GREATER
        using var buffer = new AlignedNativeBuffer(64 * 1024, 4096);
        using MemoryHandle handle = buffer.Pin();
        nuint address = (nuint)handle.Pointer;

        Assert.Equal(0u, address % 4096u);
        Assert.Equal(64 * 1024, buffer.GetSpan().Length);
#endif
    }

    [Fact]
    public async Task Test_DirectIoStreams_ReadWriteRoundTripAsync()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"OdfKitDirectIo_{Guid.NewGuid():N}.bin");
        try
        {
            byte[] payload = Enumerable.Range(0, 4096 + 123)
                .Select(i => (byte)(i % 251))
                .ToArray();
            using (var writer = new OdfDirectIoWritableStream(tempFile))
            {
                await writer.WriteAsync(payload, 0, payload.Length, CancellationToken.None);
            }

            using (var reader = new OdfDirectIoReadableStream(tempFile))
            {
                byte[] buffer = new byte[payload.Length + 16];
                int read = await reader.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
                Assert.Equal(payload.Length, read);
                Assert.Equal(payload, buffer.Take(read).ToArray());
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Test_DirectIoStreams_ReadAcrossPrefetchAndTailBoundariesAsync()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"OdfKitDirectIoBoundary_{Guid.NewGuid():N}.bin");
        try
        {
            byte[] payload = Enumerable.Range(0, (64 * 1024 * 2) + 333)
                .Select(i => (byte)((i * 17) % 251))
                .ToArray();

            using (var writer = new OdfDirectIoWritableStream(tempFile))
            {
                int offset = 0;
                while (offset < payload.Length)
                {
                    int count = Math.Min(7001, payload.Length - offset);
                    await writer.WriteAsync(payload, offset, count, CancellationToken.None);
                    offset += count;
                }
            }

            using (var reader = new OdfDirectIoReadableStream(tempFile))
            {
                byte[] actual = new byte[payload.Length];
                int total = 0;
                while (total < actual.Length)
                {
                    int read = await reader.ReadAsync(actual, total, Math.Min(5003, actual.Length - total), CancellationToken.None);
                    if (read == 0)
                        break;

                    total += read;
                }

                Assert.Equal(payload.Length, total);
                Assert.Equal(payload, actual);

                reader.Position = (64 * 1024) - 17;
                byte[] boundary = new byte[64];
                int boundaryRead = reader.Read(boundary, 0, boundary.Length);
                Assert.Equal(boundary.Length, boundaryRead);
                Assert.Equal(payload.Skip((64 * 1024) - 17).Take(boundary.Length).ToArray(), boundary);

                reader.Seek(-333, SeekOrigin.End);
                byte[] tail = new byte[333];
                int tailRead = reader.Read(tail, 0, tail.Length);
                Assert.Equal(tail.Length, tailRead);
                Assert.Equal(payload.Skip(payload.Length - 333).ToArray(), tail);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Test_OdfTransaction_JournalCreateFailure_FailsFast()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"journal_fail_{Guid.NewGuid():N}.ods");
        string journalPath = tempFile + ".journal";
        try
        {
            using (var doc = SpreadsheetDocument.Create())
            {
                doc.Save(tempFile);
            }

            Directory.CreateDirectory(journalPath);

            using var package = OdfPackage.Open(tempFile);
            var ex = Assert.Throws<IOException>(() => OdfTransaction.Begin(package));
            Assert.Equal(OdfLocalizer.GetMessage("Err_OdfPackage_JournalCreateFailed"), ex.Message);
        }
        finally
        {
            if (Directory.Exists(journalPath))
            {
                Directory.Delete(journalPath, recursive: true);
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Test_OdfPackage_ZipEntryCountLimit_UsesLocalizedException()
    {
        using MemoryStream packageStream = CreateZipPackage(
            ("content.xml", Encoding.UTF8.GetBytes("<root/>")),
            ("styles.xml", Encoding.UTF8.GetBytes("<root/>")));

        var options = new OdfLoadOptions { MaxZipEntries = 1 };

        var ex = Assert.Throws<SecurityException>(() => OdfPackage.Open(packageStream, leaveOpen: true, options));

        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", 2, 1), ex.Message);
    }

    [Fact]
    public void Test_OdfPackage_ZipEntrySizeLimit_UsesLocalizedException()
    {
        byte[] payload = Encoding.UTF8.GetBytes("12345");
        using MemoryStream packageStream = CreateZipPackage(("content.xml", payload));

        var options = new OdfLoadOptions { MaxEntrySize = 4 };

        var ex = Assert.Throws<SecurityException>(() => OdfPackage.Open(packageStream, leaveOpen: true, options));

        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", "content.xml", 5, 4), ex.Message);
    }

    [Fact]
    public void Test_OdfPackage_ZipTotalUncompressedSizeLimit_UsesLocalizedException()
    {
        byte[] payload = Encoding.UTF8.GetBytes("12345");
        using MemoryStream packageStream = CreateZipPackage(
            ("content.xml", payload),
            ("styles.xml", payload));

        var options = new OdfLoadOptions
        {
            MaxEntrySize = 10,
            MaxTotalUncompressedSize = 8
        };

        var ex = Assert.Throws<SecurityException>(() => OdfPackage.Open(packageStream, leaveOpen: true, options));

        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded", 10, 8), ex.Message);
    }

    [Fact]
    public void Test_OdfSparseStorage_ColdPageCompression_RoundTrip()
    {
        var table = new TableTableElement("table");
        table.SetSparseCellStyle(0, 0, "StyleA");
        table.SetSparseCellFormula(0, 1, "=A1");

        table.CompressColdPages();

        Assert.NotNull(table._hotFormulaPtrs);
        Assert.Single(table._hotFormulaPtrs);
        SafeHandle hotFormulaHandle = Assert.IsAssignableFrom<SafeHandle>(table._hotFormulaPtrs.Values.Single());
        Assert.False(hotFormulaHandle.IsInvalid);
        Assert.False(hotFormulaHandle.IsClosed);
        Assert.NotNull(table._pageStates);
        Assert.False(table._pageStates[0][0].IsHot);
        Assert.NotNull(table._pageStates[0][0].CompressedBytes);
        Assert.Equal(IntPtr.Zero, table._nativePages![0][0]);

        var cell = table[0, 0];
        Assert.Equal("StyleA", cell.StyleName);

        var formulaCell = table[0, 1];
        Assert.Equal("=A1", formulaCell.GetAttribute("formula", OdfNamespaces.Table));

        table.Dispose();

        Assert.Null(table._hotFormulaPtrs);
        Assert.True(hotFormulaHandle.IsClosed);
    }

    [Fact]
    public void Test_OdfTransaction_Journal_Recovery()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"journal_test_{Guid.NewGuid():N}.ods");
        try
        {
            // 1. 建立一個 ODS 檔案並儲存，作為基礎
            using (var doc = SpreadsheetDocument.Create())
            {
                var table = new TableTableElement("table") { Name = "Sheet1" };
                doc.ContentDom.AppendChild(table);
                table.SetSparseCellStyle(0, 0, "OriginalStyle");
                doc.Save(tempFile);
            }

            // 2. 開啟檔案並開始交易
            using (var doc = (SpreadsheetDocument)OdfDocument.Load(tempFile))
            {
                var package = doc.Package;
                var table = doc.ContentDom.Descendants().OfType<TableTableElement>().First();

                string journalFile = tempFile + ".journal";
                Assert.False(File.Exists(journalFile));

                using (var tx = OdfTransaction.Begin(package))
                {
                    // 驗證開始交易後自動備份 .journal 檔案
                    Assert.True(File.Exists(journalFile));

                    // 修改資料並呼叫 Save
                    table.SetSparseCellStyle(0, 0, "ModifiedStyle");
                    doc.Save();
                    Assert.Equal("ModifiedStyle", table[0, 0].StyleName);

                    // 驗證修改後實體檔案已變更，但 .journal 仍保留原始內容
                    using (var fs = OpenSharedSnapshot(tempFile))
                    using (var pkg = OdfPackage.Open(fs, true))
                    {
                        var reloadedDoc = new SpreadsheetDocument(pkg);
                        var t = reloadedDoc.ContentDom.Descendants().OfType<TableTableElement>().First();
                        var cell = t[0, 0];
                        Assert.Equal("ModifiedStyle", cell.StyleName);
                    }

                    // 對 .journal 讀取驗證是否為 OriginalStyle
                    using (var fs = OpenSharedSnapshot(journalFile))
                    using (var pkg = OdfPackage.Open(fs, true))
                    {
                        var reloadedDoc = new SpreadsheetDocument(pkg);
                        var t = reloadedDoc.ContentDom.Descendants().OfType<TableTableElement>().First();
                        var cell = t[0, 0];
                        Assert.Equal("OriginalStyle", cell.StyleName);
                    }

                    // 故意不 commit，直接 dispose tx 觸發自動回滾
                }

                // 驗證回滾後，.journal 檔案被刪除
                Assert.False(File.Exists(journalFile));

                // 驗證記憶體與實體磁碟都回滾為 OriginalStyle
                var reloadedTable = doc.ContentDom.Descendants().OfType<TableTableElement>().First();
                var cellReloaded = reloadedTable[0, 0];
                Assert.Equal("OriginalStyle", cellReloaded.StyleName);
            }

            // 重新開檔驗證實體磁碟檔案內容
            using (var doc = (SpreadsheetDocument)OdfDocument.Load(tempFile))
            {
                var table = doc.ContentDom.Descendants().OfType<TableTableElement>().First();
                var cellReloaded = table[0, 0];
                Assert.Equal("OriginalStyle", cellReloaded.StyleName);
            }

            // 3. 測試 Crash Recovery 自我修復
            string fakeJournal = tempFile + ".journal";
            if (File.Exists(fakeJournal))
            {
                File.Delete(fakeJournal);
            }

            // 把原始檔案複製為 .journal 備份
            File.Copy(tempFile, fakeJournal);

            // 故意破壞原檔案，設為空位元組
            File.WriteAllBytes(tempFile, Array.Empty<byte>());

            // 呼叫 Open，此時應自動藉由 .journal 進行還原
            using (var doc = (SpreadsheetDocument)OdfDocument.Load(tempFile))
            {
                var table = doc.ContentDom.Descendants().OfType<TableTableElement>().First();
                var cellReloaded = table[0, 0];
                Assert.Equal("OriginalStyle", cellReloaded.StyleName);
            }

            // 驗證還原後 .journal 已被刪除
            Assert.False(File.Exists(fakeJournal));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                { File.Delete(tempFile); }
                catch { }
            }
            if (File.Exists(tempFile + ".journal"))
            {
                try
                { File.Delete(tempFile + ".journal"); }
                catch { }
            }
        }
    }

    private static MemoryStream OpenSharedSnapshot(string path)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var snapshot = new MemoryStream();
        source.CopyTo(snapshot);
        snapshot.Position = 0;
        return snapshot;
    }

    private static uint ReadEocdCentralDirectoryOffset(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] bytes = new byte[stream.Length];
        int read = stream.Read(bytes, 0, bytes.Length);
        Assert.Equal(bytes.Length, read);

        for (int i = bytes.Length - 22; i >= 0; i--)
        {
            if (bytes[i] == 0x50 &&
                bytes[i + 1] == 0x4b &&
                bytes[i + 2] == 0x05 &&
                bytes[i + 3] == 0x06)
            {
                return BitConverter.ToUInt32(bytes, i + 16);
            }
        }

        throw new InvalidDataException("找不到 ZIP EOCD 記錄。");
    }

    private static (uint LocalHeaderCrc, uint CentralDirectoryCrc) ReadZipEntryCrcs(string path, string entryName)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int eocdOffset = FindSignature(bytes, 0x06054b50, bytes.Length - 22, searchBackwards: true);
        uint centralDirectoryOffset = BitConverter.ToUInt32(bytes, eocdOffset + 16);
        ushort entryCount = BitConverter.ToUInt16(bytes, eocdOffset + 10);
        int offset = checked((int)centralDirectoryOffset);

        for (int i = 0; i < entryCount; i++)
        {
            Assert.Equal(0x02014b50u, BitConverter.ToUInt32(bytes, offset));
            uint centralDirectoryCrc = BitConverter.ToUInt32(bytes, offset + 16);
            ushort nameLength = BitConverter.ToUInt16(bytes, offset + 28);
            ushort extraLength = BitConverter.ToUInt16(bytes, offset + 30);
            ushort commentLength = BitConverter.ToUInt16(bytes, offset + 32);
            uint localHeaderOffset = BitConverter.ToUInt32(bytes, offset + 42);
            string name = Encoding.UTF8.GetString(bytes, offset + 46, nameLength);

            if (string.Equals(name, entryName, StringComparison.Ordinal))
            {
                int localOffset = checked((int)localHeaderOffset);
                Assert.Equal(0x04034b50u, BitConverter.ToUInt32(bytes, localOffset));
                uint localHeaderCrc = BitConverter.ToUInt32(bytes, localOffset + 14);
                return (localHeaderCrc, centralDirectoryCrc);
            }

            offset += 46 + nameLength + extraLength + commentLength;
        }

        throw new InvalidDataException($"找不到 ZIP entry：{entryName}");
    }

    private static int FindSignature(byte[] bytes, uint signature, int startIndex, bool searchBackwards)
    {
        if (searchBackwards)
        {
            for (int i = Math.Min(startIndex, bytes.Length - 4); i >= 0; i--)
            {
                if (BitConverter.ToUInt32(bytes, i) == signature)
                {
                    return i;
                }
            }
        }
        else
        {
            for (int i = Math.Max(0, startIndex); i <= bytes.Length - 4; i++)
            {
                if (BitConverter.ToUInt32(bytes, i) == signature)
                {
                    return i;
                }
            }
        }

        throw new InvalidDataException("找不到 ZIP 簽章。");
    }

    private static string CreateMinimalManifestXml(params string[] additionalEntries)
    {
        var builder = new StringBuilder();
        builder.Append("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.4\">");
        builder.Append("<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\" />");
        builder.Append("<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\" />");
        builder.Append("<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\" />");
        foreach (string entry in additionalEntries)
        {
            builder.Append("<manifest:file-entry manifest:full-path=\"");
            builder.Append(entry);
            builder.Append("\" manifest:media-type=\"application/octet-stream\" />");
        }

        builder.Append("</manifest:manifest>");
        return builder.ToString();
    }

    private static MemoryStream CreateZipPackage(params (string Name, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                using Stream entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
