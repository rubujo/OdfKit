using System.Buffers.Binary;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfZipMimeTypeConformanceTests
{
    [Theory]
    [InlineData(OdfVersion.Odf12, false, false)]
    [InlineData(OdfVersion.Odf12, true, false)]
    [InlineData(OdfVersion.Odf12, false, true)]
    [InlineData(OdfVersion.Odf12, true, true)]
    [InlineData(OdfVersion.Odf13, false, false)]
    [InlineData(OdfVersion.Odf13, true, false)]
    [InlineData(OdfVersion.Odf13, false, true)]
    [InlineData(OdfVersion.Odf13, true, true)]
    [InlineData(OdfVersion.Odf14, false, false)]
    [InlineData(OdfVersion.Odf14, true, false)]
    [InlineData(OdfVersion.Odf14, false, true)]
    [InlineData(OdfVersion.Odf14, true, true)]
    public async Task UnsupportedCompressionIsRejectedForLazyBinaryPayloads(OdfVersion version, bool useFile, bool asynchronous)
    {
        using var original = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(original, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry("payload.bin", new byte[] { 1, 2, 3, 4 }, "application/octet-stream");
            writer.Save();
        }
        byte[] bytes = original.ToArray();
        int position = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6)));
        bool changed = false;
        while (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position)) == 0x02014b50)
        {
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 28));
            if (Encoding.UTF8.GetString(bytes, position + 46, nameLength) == "payload.bin")
            {
                int local = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position + 42)));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position + 10), 99);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(local + 8), 99);
                changed = true;
            }
            position += 46 + nameLength + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 30))
                + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 32));
        }
        Assert.True(changed);
        await AssertExtraFieldIssue(bytes, true, version, useFile, asynchronous, true, "ODF0007");
    }

    [Theory]
    [InlineData("application/vnd.oasis.opendocument.text", false)]
    [InlineData("application/vnd.oasis.opendocument.text\n", true)]
    [InlineData(" application/vnd.oasis.opendocument.text", true)]
    [InlineData("\ufeffapplication/vnd.oasis.opendocument.text", true)]
    public void MimeTypeEncodingIsValidatedBeforeReaderNormalization(string text, bool invalid)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, OdfVersion.Odf14, true))
        {
            writer.WriteEntry("mimetype", Encoding.UTF8.GetBytes(text));
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        Assert.Equal("application/vnd.oasis.opendocument.text", package.MimeType);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);
        if (invalid)
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0006");
        else
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF0006");
    }

    public static IEnumerable<object[]> LoadCases()
    {
        foreach (OdfVersion version in new[] { OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
            foreach (bool useFile in new[] { false, true })
                foreach (bool asynchronous in new[] { false, true })
                    foreach (bool lazy in new[] { false, true })
                        yield return [version, useFile, asynchronous, lazy];
    }

    [Theory]
    [MemberData(nameof(LoadCases))]
    public async Task MimeTypeOrderUsesLocalHeadersInsteadOfCentralDirectory(OdfVersion version, bool useFile, bool asynchronous, bool lazy)
    {
        using var original = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(original, OdfDocumentKind.Text, version, true))
            writer.Save();
        byte[] valid = original.ToArray();
        await AssertExtraFieldIssue(ReorderFirstTwoEntries(valid, local: true), true, version, useFile, asynchronous, lazy, "ODF0003");
        await AssertExtraFieldIssue(ReorderFirstTwoEntries(valid, local: false), false, version, useFile, asynchronous, lazy, "ODF0003");
    }

    [Theory]
    [MemberData(nameof(LoadCases))]
    public async Task MimeTypeLocalExtraFieldIsRejectedAcrossLoadPaths(OdfVersion version, bool useFile, bool asynchronous, bool lazy)
    {
        using var original = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(original, OdfDocumentKind.Text, version, true))
            writer.Save();

        byte[] valid = original.ToArray();
        await AssertExtraFieldIssue(valid, false, version, useFile, asynchronous, lazy);
        await AssertExtraFieldIssue(AddExtraField(valid, local: true), true, version, useFile, asynchronous, lazy);
        await AssertExtraFieldIssue(ReorderFirstTwoEntries(AddExtraField(valid, local: true), local: true), true, version, useFile, asynchronous, lazy);
        // 規範限制的是 local header；中央目錄自己的 extra field 不構成這項違規。
        await AssertExtraFieldIssue(AddExtraField(valid, local: false), false, version, useFile, asynchronous, lazy);
    }

    private static async Task AssertExtraFieldIssue(byte[] bytes, bool expected, OdfVersion version, bool useFile, bool asynchronous, bool lazy, string ruleId = "ODF0005")
    {
        string path = Path.Combine(Path.GetTempPath(), $"odf-mimetype-{Guid.NewGuid():N}.odt");
        using var stream = new MemoryStream(bytes);
        var options = new OdfLoadOptions { AllowLazyLoading = lazy };
        try
        {
            if (useFile)
                await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

            using OdfPackage package = useFile
                ? asynchronous
                    ? await OdfPackage.OpenAsync(path, options, TestContext.Current.CancellationToken)
                    : OdfPackage.Open(path, options)
                : asynchronous
                    ? await OdfPackage.OpenAsync(stream, true, options, TestContext.Current.CancellationToken)
                    : OdfPackage.Open(stream, true, options);
            OdfComplianceProfile profile = version switch
            {
                OdfVersion.Odf10 => OdfComplianceProfiles.OasisOdf10,
                OdfVersion.Odf11 => OdfComplianceProfiles.OasisOdf11,
                OdfVersion.Odf12 => OdfComplianceProfiles.OasisOdf12Extended,
                OdfVersion.Odf13 => OdfComplianceProfiles.OasisOdf13Extended,
                _ => OdfComplianceProfiles.OasisOdf14Extended
            };
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            if (expected)
            {
                string expectedPath = ruleId == "ODF0007" ? "payload.bin" : "mimetype";
                Assert.Contains(report.Issues, issue => issue.RuleId == ruleId && issue.PackagePath == expectedPath);
                OdfIssueSeverity severity = ruleId == "ODF0003" && version is OdfVersion.Odf10 or OdfVersion.Odf11
                    ? OdfIssueSeverity.Warning : OdfIssueSeverity.Error;
                Assert.Contains(report.Issues, issue => issue.RuleId == ruleId && issue.Severity == severity);
            }
            else
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == ruleId);
        }
        finally
        {
            if (useFile)
                File.Delete(path);
        }
    }

    private static byte[] ReorderFirstTwoEntries(byte[] source, bool local)
    {
        int central = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(source.Length - 6)));
        var records = new List<(int Position, int Length, uint Offset)>();
        int position = central;
        while (BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(position)) == 0x02014b50)
        {
            int length = 46 + BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(position + 28))
                + BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(position + 30))
                + BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(position + 32));
            records.Add((position, length, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(position + 42))));
            position += length;
        }
        Assert.True(records.Count >= 2);
        Assert.Equal(0U, records[0].Offset);
        byte[] result = (byte[])source.Clone();
        if (local)
        {
            int firstLength = checked((int)records[1].Offset);
            int secondEnd = records.Count > 2 ? checked((int)records[2].Offset) : central;
            int secondLength = secondEnd - firstLength;
            source.AsSpan(firstLength, secondLength).CopyTo(result);
            source.AsSpan(0, firstLength).CopyTo(result.AsSpan(secondLength));
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(records[0].Position + 42), (uint)secondLength);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(records[1].Position + 42), 0);
        }
        else
        {
            source.AsSpan(records[1].Position, records[1].Length).CopyTo(result.AsSpan(central));
            source.AsSpan(central, records[0].Length).CopyTo(result.AsSpan(central + records[1].Length));
        }
        return result;
    }

    private static byte[] AddExtraField(byte[] source, bool local)
    {
        int eocd = source.Length - 22;
        Assert.Equal(0x06054b50U, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(eocd)));
        int central = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(eocd + 16)));
        int insertion = local ? 38 : central + 46 + 8;
        byte[] result = new byte[source.Length + 4];
        source.AsSpan(0, insertion).CopyTo(result);
        // 未知、零長度的合法 ZIP extra field，避免混入 ZIP64 等額外語意。
        result[insertion] = 0xfe;
        result[insertion + 1] = 0xca;
        source.AsSpan(insertion).CopyTo(result.AsSpan(insertion + 4));

        if (local)
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(28), 4);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(central + 30), 4);

        int newCentral = central + (local ? 4 : 0);
        int position = newCentral;
        while (BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(position)) == 0x02014b50)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(position + 42));
            if (local && offset >= insertion)
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(position + 42), offset + 4);
            position += 46 + BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(position + 28))
                + BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(position + 30))
                + BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(position + 32));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(eocd + 4 + 16), (uint)newCentral);
        if (!local)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(eocd + 12));
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(eocd + 4 + 12), size + 4);
        }
        return result;
    }
}
