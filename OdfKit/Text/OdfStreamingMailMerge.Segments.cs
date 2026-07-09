using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace OdfKit.Text;

/// <summary>
/// Partial: mail-merge template segment model (static, placeholder, foreach).
/// Partial：郵件合併範本區段模型（靜態、預留位置、foreach）。
/// </summary>
public static partial class OdfStreamingMailMerge
{
    private abstract class TemplateSegment
    {
        public abstract Task WriteToAsync(Stream stream, IDictionary<string, object?> data, Dictionary<string, object?> localContext, CancellationToken cancellationToken);
    }

    private sealed class StaticSegment : TemplateSegment
    {
        private readonly byte[] _bytes;

        public StaticSegment(byte[] bytes)
        {
            _bytes = bytes;
        }

        public override Task WriteToAsync(Stream stream, IDictionary<string, object?> data, Dictionary<string, object?> localContext, CancellationToken cancellationToken)
        {
            if (_bytes.Length == 0)
                return Task.CompletedTask;
            return stream.WriteAsync(_bytes, 0, _bytes.Length, cancellationToken);
        }
    }

    private sealed class PlaceholderSegment : TemplateSegment
    {
        private readonly string _path;

        public PlaceholderSegment(string path)
        {
            _path = path;
        }

        public override Task WriteToAsync(Stream stream, IDictionary<string, object?> data, Dictionary<string, object?> localContext, CancellationToken cancellationToken)
        {
            object? val = GetValueWithPath(data, _path, localContext);
            if (val is null)
                return Task.CompletedTask;

            string text = val.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return Task.CompletedTask;

            WriteXmlEscapedUtf8(stream, text);
            return Task.CompletedTask;
        }
    }

    private sealed class ForeachSegment : TemplateSegment
    {
        private readonly string _itemName;
        private readonly string _collectionName;
        private readonly List<TemplateSegment> _body;

        public ForeachSegment(string itemName, string collectionName, List<TemplateSegment> body)
        {
            _itemName = itemName;
            _collectionName = collectionName;
            _body = body;
        }

        public override async Task WriteToAsync(Stream stream, IDictionary<string, object?> data, Dictionary<string, object?> localContext, CancellationToken cancellationToken)
        {
            object? colObj = null;
            if (localContext.TryGetValue(_collectionName, out var localCol))
            {
                colObj = localCol;
            }
            else
            {
                colObj = MailMergeExpressionCache.GetValue(data, _collectionName);
            }

            if (colObj is IEnumerable enumerable && colObj is not string)
            {
                foreach (var item in enumerable)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item is null)
                        continue;

                    var childContext = new Dictionary<string, object?>(localContext, StringComparer.OrdinalIgnoreCase);
                    childContext[_itemName] = item;

                    foreach (var segment in _body)
                    {
                        await segment.WriteToAsync(stream, data, childContext, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private sealed class PrecompiledBatchTemplate
    {
        public List<TemplateSegment> HeaderSegments { get; }
        public List<TemplateSegment> BodySegments { get; }
        public List<TemplateSegment> FooterSegments { get; }

        public PrecompiledBatchTemplate(List<TemplateSegment> header, List<TemplateSegment> body, List<TemplateSegment> footer)
        {
            HeaderSegments = header;
            BodySegments = body;
            FooterSegments = footer;
        }
    }

    private sealed class XmlNodeInfo
    {
        public XmlNodeType NodeType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string NamespaceUri { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsEmpty { get; set; }
        public List<XmlAttributeInfo> Attributes { get; set; } = new();
    }

    private sealed class XmlAttributeInfo
    {
        public string Name { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string NamespaceUri { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

}
