using System;
using System.IO;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// 以固定大小緩衝區讀取受長度限制的文字行，避免先配置不受限的完整資料行。
/// </summary>
internal sealed class OdfBoundedLineReader
{
    private const int BufferSize = 1_024;
    private readonly char[] _buffer = new char[BufferSize];
    private readonly TextReader _reader;
    private int _bufferLength;
    private int _bufferPosition;
    private bool _endOfInput;

    internal OdfBoundedLineReader(TextReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// 讀取下一行，並在配置超過上限的內容前拒絕輸入。
    /// </summary>
    internal string? ReadLine()
    {
        StringBuilder? builder = null;
        int lineLength = 0;
        while (EnsureBuffered())
        {
            int segmentStart = _bufferPosition;
            while (_bufferPosition < _bufferLength &&
                   _buffer[_bufferPosition] is not ('\r' or '\n'))
            {
                _bufferPosition++;
            }

            int segmentLength = _bufferPosition - segmentStart;
            if (segmentLength > OdfCodePointMappingTable.MaxLineLength - lineLength)
            {
                OdfCodePointMappingTable.EnsureLineLength(
                    new string(' ', OdfCodePointMappingTable.MaxLineLength + 1));
            }

            lineLength += segmentLength;
            bool foundLineEnding = _bufferPosition < _bufferLength;
            if (builder is null && foundLineEnding)
            {
                string line = new(_buffer, segmentStart, segmentLength);
                ConsumeLineEnding();
                return line;
            }

            builder ??= new StringBuilder(Math.Min(OdfCodePointMappingTable.MaxLineLength, BufferSize * 2));
            builder.Append(_buffer, segmentStart, segmentLength);
            if (foundLineEnding)
            {
                ConsumeLineEnding();
                return builder.ToString();
            }
        }

        return builder?.ToString();
    }

    private bool EnsureBuffered()
    {
        if (_bufferPosition < _bufferLength)
        {
            return true;
        }

        if (_endOfInput)
        {
            return false;
        }

        _bufferLength = _reader.Read(_buffer, 0, _buffer.Length);
        _bufferPosition = 0;
        _endOfInput = _bufferLength == 0;
        return !_endOfInput;
    }

    private void ConsumeLineEnding()
    {
        char lineEnding = _buffer[_bufferPosition++];
        if (lineEnding == '\r' && EnsureBuffered() && _buffer[_bufferPosition] == '\n')
        {
            _bufferPosition++;
        }
    }
}
