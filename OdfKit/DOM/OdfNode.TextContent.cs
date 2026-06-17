using System.Buffers;
using System.Text;
using OdfKit.Core;

namespace OdfKit.DOM;

public partial class OdfNode
{
    /// <summary>
    /// 將節點的文字內容寫入指定的緩衝區寫入器，避免配置完整 <see cref="string"/>。
    /// </summary>
    /// <param name="buffer">接收文字內容的緩衝區寫入器</param>
    /// <returns>若 <paramref name="buffer"/> 不為 <see langword="null"/> 則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool TryWriteTextContent(IBufferWriter<char> buffer)
    {
        if (buffer is null)
        {
            return false;
        }

        WriteTextContentTo(new BufferWriterTextSink(buffer));
        return true;
    }

    private void WriteTextContentTo(ITextContentSink sink)
    {
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            if (_value is not null && _value.Length > 0)
            {
                sink.Append(_value);
            }

            return;
        }

        if (Children.Count == 0)
        {
            return;
        }

        if (Children.Count == 1)
        {
            Children[0].WriteTextContentTo(sink);
            return;
        }

        foreach (var child in Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Text)
            {
                if (child.LocalName == "line-break")
                {
                    sink.Append('\n');
                    continue;
                }

                if (child.LocalName == "tab")
                {
                    sink.Append('\t');
                    continue;
                }

                if (child.LocalName == "s")
                {
                    int count = 1;
                    string? cAttr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (cAttr is not null && int.TryParse(cAttr, out var parsedCount))
                    {
                        count = parsedCount;
                    }

                    sink.Append(' ', count);
                    continue;
                }
            }

            child.WriteTextContentTo(sink);
        }
    }

    private interface ITextContentSink
    {
        void Append(char value);

        void Append(string value);

        void Append(char value, int repeat);
    }

    private sealed class StringBuilderTextSink(StringBuilder builder) : ITextContentSink
    {
        public void Append(char value) => builder.Append(value);

        public void Append(string value)
        {
            if (value.Length > 0)
            {
                builder.Append(value);
            }
        }

        public void Append(char value, int repeat)
        {
            for (int i = 0; i < repeat; i++)
            {
                builder.Append(value);
            }
        }
    }

    private sealed class BufferWriterTextSink(IBufferWriter<char> buffer) : ITextContentSink
    {
        public void Append(char value)
        {
            Span<char> span = buffer.GetSpan(1);
            span[0] = value;
            buffer.Advance(1);
        }

        public void Append(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            Span<char> span = buffer.GetSpan(value.Length);
            value.AsSpan().CopyTo(span);
            buffer.Advance(value.Length);
        }

        public void Append(char value, int repeat)
        {
            if (repeat <= 0)
            {
                return;
            }

            while (repeat > 0)
            {
                int chunk = Math.Min(repeat, 256);
                Span<char> span = buffer.GetSpan(chunk);
                span.Fill(value);
                buffer.Advance(chunk);
                repeat -= chunk;
            }
        }
    }
}
