using System.Buffers;
using BenchmarkDotNet.Attributes;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Benchmarks;

/// <summary>
/// <see cref="OdfNode.TextContent"/> 與 <see cref="OdfNode.TryWriteTextContent"/> 效能基準。
/// </summary>
[MemoryDiagnoser]
public class DomTextContentBenchmarks
{
    private OdfNode _paragraph = null!;
    private ArrayBufferWriter<char> _bufferWriter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        for (int i = 0; i < 50; i++)
        {
            _paragraph.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
            {
                TextContent = $"段落片段 {i} ",
            });
            _paragraph.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
        }

        _paragraph.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
        {
            TextContent = "結尾",
        });
        _bufferWriter = new ArrayBufferWriter<char>();
    }

    [Benchmark(Baseline = true)]
    public string ReadTextContent()
    {
        return _paragraph.TextContent;
    }

    [Benchmark]
    public int WriteTextContentToBuffer()
    {
        _bufferWriter.Clear();
        _paragraph.TryWriteTextContent(_bufferWriter);
        return _bufferWriter.WrittenCount;
    }
}
