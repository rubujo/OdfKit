using System;

namespace OdfKit.Compliance;

/// <summary>
/// 識別命名空間限定的 ODF 名稱，而不依賴 XML 前綴。
/// </summary>
public sealed class OdfQualifiedName : IEquatable<OdfQualifiedName>
{
    /// <summary>
    /// 初始化限定名稱的新執行個體。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域元素或屬性名稱</param>
    public OdfQualifiedName(string namespaceUri, string localName)
    {
        if (string.IsNullOrWhiteSpace(namespaceUri))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfQualifiedName_NamespaceCannotBeEmpty"), nameof(namespaceUri));
        if (string.IsNullOrWhiteSpace(localName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfQualifiedName_LocalCannotBeEmpty"), nameof(localName));
        NamespaceUri = namespaceUri;
        LocalName = localName;
    }

    /// <summary>
    /// 取得命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// 取得區域元素或屬性名稱。
    /// </summary>
    public string LocalName { get; }

    /// <inheritdoc />
    public bool Equals(OdfQualifiedName? other)
    {
        return other is not null &&
            string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
            string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as OdfQualifiedName);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(NamespaceUri) * 397) ^
                StringComparer.Ordinal.GetHashCode(LocalName);
        }
    }

    /// <inheritdoc />
    public override string ToString() => "{" + NamespaceUri + "}" + LocalName;
}

