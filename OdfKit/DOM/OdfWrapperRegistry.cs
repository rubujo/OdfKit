using System;
using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// 提供自訂 XML 擴充元素對應 typed DOM wrapper 的註冊表。
/// </summary>
public static class OdfWrapperRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<OdfWrapperKey, Func<string?, OdfElement>> Factories = new();

    /// <summary>
    /// 註冊指定元素名稱對應的 typed DOM wrapper factory。
    /// </summary>
    /// <param name="localName">元素局部名稱</param>
    /// <param name="namespaceUri">元素命名空間 URI</param>
    /// <param name="factory">依命名空間前綴建立 wrapper 的 factory</param>
    public static void Register(string localName, string namespaceUri, Func<string?, OdfElement> factory)
    {
        if (localName is null)
            throw new ArgumentNullException(nameof(localName));
        if (namespaceUri is null)
            throw new ArgumentNullException(nameof(namespaceUri));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        lock (SyncRoot)
        {
            Factories[new OdfWrapperKey(namespaceUri, localName)] = factory;
        }
    }

    /// <summary>
    /// 移除指定元素名稱的自訂 typed DOM wrapper 註冊。
    /// </summary>
    /// <param name="localName">元素局部名稱</param>
    /// <param name="namespaceUri">元素命名空間 URI</param>
    /// <returns>若已移除既有註冊，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public static bool Unregister(string localName, string namespaceUri)
    {
        if (localName is null)
            throw new ArgumentNullException(nameof(localName));
        if (namespaceUri is null)
            throw new ArgumentNullException(nameof(namespaceUri));

        lock (SyncRoot)
        {
            return Factories.Remove(new OdfWrapperKey(namespaceUri, localName));
        }
    }

    internal static OdfElement? CreateElement(string localName, string namespaceUri, string? prefix)
    {
        Func<string?, OdfElement>? factory;
        lock (SyncRoot)
        {
            if (!Factories.TryGetValue(new OdfWrapperKey(namespaceUri, localName), out factory))
            {
                return null;
            }
        }

        OdfElement element = factory(prefix);
        if (element.LocalName != localName || element.NamespaceUri != namespaceUri)
        {
            return new OdfElement(localName, namespaceUri, prefix);
        }

        element.Prefix = prefix;
        return element;
    }

    private readonly struct OdfWrapperKey : IEquatable<OdfWrapperKey>
    {
        public OdfWrapperKey(string namespaceUri, string localName)
        {
            NamespaceUri = namespaceUri;
            LocalName = localName;
        }

        private string NamespaceUri { get; }

        private string LocalName { get; }

        public bool Equals(OdfWrapperKey other)
            => string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
               string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);

        public override bool Equals(object? obj)
            => obj is OdfWrapperKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(NamespaceUri);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(LocalName);
                return hash;
            }
        }
    }
}
