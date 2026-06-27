using System;
using System.Buffers;
using System.Buffers.Text;

namespace OdfKit.DOM;

internal readonly ref struct OdfUtf8SpanWriter
{
    private readonly IBufferWriter<byte> _writer;

    public OdfUtf8SpanWriter(IBufferWriter<byte> writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void WriteAscii(ReadOnlySpan<byte> value)
        => WriteRaw(value);

    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (!Utf8Formatter.TryFormat(value, buffer, out int written))
        {
            throw new InvalidOperationException();
        }

        WriteRaw(buffer.Slice(0, written));
    }

    public void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!Utf8Formatter.TryFormat(value, buffer, out int written, new StandardFormat('G')))
        {
            throw new InvalidOperationException();
        }

        WriteRaw(buffer.Slice(0, written));
    }

    public void WriteBoolean(bool value)
        => WriteRaw(value ? "true"u8 : "false"u8);

    public void WriteDateTime(DateTime value)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!Utf8Formatter.TryFormat(value, buffer, out int written, new StandardFormat('O')))
        {
            throw new InvalidOperationException();
        }

        WriteRaw(buffer.Slice(0, written));
    }

    public void WriteEscapedText(ReadOnlySpan<char> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            switch (ch)
            {
                case '&':
                    WriteRaw("&amp;"u8);
                    break;
                case '<':
                    WriteRaw("&lt;"u8);
                    break;
                case '>':
                    WriteRaw("&gt;"u8);
                    break;
                default:
                    if (char.IsHighSurrogate(ch) &&
                        i + 1 < value.Length &&
                        char.IsLowSurrogate(value[i + 1]))
                    {
                        WriteUtf8Scalar(char.ConvertToUtf32(ch, value[++i]));
                    }
                    else
                    {
                        WriteUtf8Scalar(ch);
                    }

                    break;
            }
        }
    }

    public void WriteEscapedAttributeValue(ReadOnlySpan<char> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            switch (ch)
            {
                case '&':
                    WriteRaw("&amp;"u8);
                    break;
                case '<':
                    WriteRaw("&lt;"u8);
                    break;
                case '"':
                    WriteRaw("&quot;"u8);
                    break;
                case '\'':
                    WriteRaw("&apos;"u8);
                    break;
                default:
                    if (char.IsHighSurrogate(ch) &&
                        i + 1 < value.Length &&
                        char.IsLowSurrogate(value[i + 1]))
                    {
                        WriteUtf8Scalar(char.ConvertToUtf32(ch, value[++i]));
                    }
                    else
                    {
                        WriteUtf8Scalar(ch);
                    }

                    break;
            }
        }
    }

    private void WriteUtf8Scalar(int scalar)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (scalar <= 0x7F)
        {
            buffer[0] = (byte)scalar;
            WriteRaw(buffer.Slice(0, 1));
        }
        else if (scalar <= 0x7FF)
        {
            buffer[0] = (byte)(0xC0 | (scalar >> 6));
            buffer[1] = (byte)(0x80 | (scalar & 0x3F));
            WriteRaw(buffer.Slice(0, 2));
        }
        else if (scalar <= 0xFFFF)
        {
            buffer[0] = (byte)(0xE0 | (scalar >> 12));
            buffer[1] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
            buffer[2] = (byte)(0x80 | (scalar & 0x3F));
            WriteRaw(buffer.Slice(0, 3));
        }
        else
        {
            buffer[0] = (byte)(0xF0 | (scalar >> 18));
            buffer[1] = (byte)(0x80 | ((scalar >> 12) & 0x3F));
            buffer[2] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
            buffer[3] = (byte)(0x80 | (scalar & 0x3F));
            WriteRaw(buffer.Slice(0, 4));
        }
    }

    private void WriteRaw(ReadOnlySpan<byte> value)
    {
        Span<byte> destination = _writer.GetSpan(value.Length);
        value.CopyTo(destination);
        _writer.Advance(value.Length);
    }
}
