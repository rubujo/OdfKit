using System.Data;
using OdfKit.WebFonts.Data.SqlServer;
using OdfKit.WebFonts.Encoding.Legacy;

namespace OdfKit.WebFonts.Tests;

public sealed class SqlServerTextReaderTests
{
    [Fact]
    public void Reader_PreservesNvarcharSupplementaryAndIvsText()
    {
        using DataTableReader reader = CreateReader("邉\U000E0110\U00020000", [0xA4, 0xA4]);

        WebFontTextSequence sequence = SqlServerWebFontTextReader.ReadProviderDecodedText(reader, 0);

        Assert.Equal([0x9089, 0xE0110, 0x20000], sequence.UnicodeScalars);
    }

    [Fact]
    public void Reader_DecodesBoundedBig5Varbinary()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        byte[] big5 = System.Text.Encoding.GetEncoding(950).GetBytes("中文");
        using DataTableReader reader = CreateReader("unused", big5);

        WebFontTextSequence sequence = SqlServerWebFontTextReader.ReadLegacyBytes(
            reader,
            1,
            new Big5CharacterMappingProvider(),
            1024);

        Assert.Equal("中文", sequence.Text);
    }

    private static DataTableReader CreateReader(string unicodeText, byte[] legacyBytes)
    {
        var table = new DataTable();
        table.Columns.Add("UnicodeText", typeof(string));
        table.Columns.Add("LegacyBytes", typeof(byte[]));
        table.Rows.Add(unicodeText, legacyBytes);
        DataTableReader reader = table.CreateDataReader();
        Assert.True(reader.Read());
        return reader;
    }
}
