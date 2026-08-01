using System.Data;
using System.Collections;
using System.Data.Common;
using OdfKit.WebFonts.Data.SqlServer;
using OdfKit.WebFonts.Encoding.Legacy;

namespace OdfKit.WebFonts.Tests;

public sealed class SqlServerTextReaderTests
{
    [Fact]
    public void ReaderPreservesNvarcharSupplementaryAndIvsText()
    {
        using DataTableReader reader = CreateReader("邉\U000E0110\U00020000", [0xA4, 0xA4]);

        WebFontTextSequence sequence = SqlServerWebFontTextReader.ReadProviderDecodedText(reader, 0);

        Assert.Equal([0x9089, 0xE0110, 0x20000], sequence.UnicodeScalars);
    }

    [Fact]
    public void ReaderDecodesBoundedBig5Varbinary()
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

    [Fact]
    public void ReaderContinuesWhenProviderReturnsPartialChunks()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        byte[] big5 = System.Text.Encoding.GetEncoding(950).GetBytes("中文測試");
        using var reader = new ChunkedBytesReader(big5, maximumChunkBytes: 2);

        WebFontTextSequence sequence = SqlServerWebFontTextReader.ReadLegacyBytes(
            reader,
            0,
            new Big5CharacterMappingProvider(),
            1024);

        Assert.Equal("中文測試", sequence.Text);
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

    private sealed class ChunkedBytesReader(byte[] value, int maximumChunkBytes) : DbDataReader
    {
        public override int FieldCount => 1;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override bool HasRows => true;
        public override object this[int ordinal] => value;
        public override object this[string name] => value;

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        {
            if (buffer is null)
            {
                return value.LongLength;
            }

            int available = Math.Max(0, value.Length - checked((int)dataOffset));
            int count = Math.Min(Math.Min(available, length), maximumChunkBytes);
            Array.Copy(value, checked((int)dataOffset), buffer, bufferOffset, count);
            return count;
        }

        public override bool IsDBNull(int ordinal) => false;
        public override bool Read() => true;
        public override bool NextResult() => false;
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override string GetName(int ordinal) => "Value";
        public override int GetOrdinal(string name) => 0;
        public override Type GetFieldType(int ordinal) => typeof(byte[]);
        public override string GetDataTypeName(int ordinal) => nameof(Byte);
        public override object GetValue(int ordinal) => value;
        public override int GetValues(object[] values)
        {
            values[0] = value;
            return 1;
        }

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
    }
}
