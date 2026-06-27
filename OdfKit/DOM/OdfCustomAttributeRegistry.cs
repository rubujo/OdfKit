using System;
using System.Collections.Generic;
using System.Text;

namespace OdfKit.DOM;

/// <summary>
/// 提供自訂 XML 屬性的 UTF-8 快速查表註冊表。
/// </summary>
public static class OdfCustomAttributeRegistry
{
    private static readonly object SyncRoot = new();
    private static List<OdfCustomAttributeEntry> _entries = [];

    /// <summary>
    /// 註冊需要在 UTF-8 解析階段快速識別的自訂 XML 屬性。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    /// <returns>釋放後會移除此註冊的物件</returns>
    /// <exception cref="ArgumentException">當 <paramref name="localName"/> 或 <paramref name="namespaceUri"/> 為空白時擲出</exception>
    public static IDisposable Register(string localName, string namespaceUri)
    {
        if (string.IsNullOrWhiteSpace(localName))
        {
            throw new ArgumentException(null, nameof(localName));
        }

        if (string.IsNullOrWhiteSpace(namespaceUri))
        {
            throw new ArgumentException(null, nameof(namespaceUri));
        }

        OdfCustomAttributeEntry entry = new(localName, namespaceUri);
        lock (SyncRoot)
        {
            List<OdfCustomAttributeEntry> next = new(_entries);
            next.Add(entry);
            _entries = next;
        }

        return new Registration(entry);
    }

    internal static bool TryMatchLocalName(ReadOnlySpan<byte> attributeName, out string localName)
    {
        localName = string.Empty;
        List<OdfCustomAttributeEntry> snapshot = _entries;
        if (snapshot.Count == 0)
        {
            return false;
        }

        int colonIndex = attributeName.IndexOf((byte)':');
        ReadOnlySpan<byte> localNameSpan = colonIndex >= 0
            ? attributeName.Slice(colonIndex + 1)
            : attributeName;

        foreach (OdfCustomAttributeEntry entry in snapshot)
        {
            if (localNameSpan.SequenceEqual(entry.LocalNameUtf8))
            {
                localName = entry.LocalName;
                return true;
            }
        }

        return false;
    }

    internal static bool IsRegistered(string localName, string namespaceUri)
    {
        List<OdfCustomAttributeEntry> snapshot = _entries;
        foreach (OdfCustomAttributeEntry entry in snapshot)
        {
            if (string.Equals(entry.LocalName, localName, StringComparison.Ordinal) &&
                string.Equals(entry.NamespaceUri, namespaceUri, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Registration : IDisposable
    {
        private OdfCustomAttributeEntry? _entry;

        public Registration(OdfCustomAttributeEntry entry) => _entry = entry;

        public void Dispose()
        {
            OdfCustomAttributeEntry? entry = _entry;
            if (entry is null)
            {
                return;
            }

            lock (SyncRoot)
            {
                List<OdfCustomAttributeEntry> next = new(_entries);
                next.Remove(entry);
                _entries = next;
            }

            _entry = null;
        }
    }

    private sealed class OdfCustomAttributeEntry : IEquatable<OdfCustomAttributeEntry>
    {
        public OdfCustomAttributeEntry(string localName, string namespaceUri)
        {
            LocalName = localName;
            NamespaceUri = namespaceUri;
            LocalNameUtf8 = Encoding.UTF8.GetBytes(localName);
        }

        public string LocalName { get; }

        public string NamespaceUri { get; }

        public byte[] LocalNameUtf8 { get; }

        public bool Equals(OdfCustomAttributeEntry? other)
            => other is not null &&
               string.Equals(LocalName, other.LocalName, StringComparison.Ordinal) &&
               string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as OdfCustomAttributeEntry);

        public override int GetHashCode() => HashCode.Combine(LocalName, NamespaceUri);
    }
}
