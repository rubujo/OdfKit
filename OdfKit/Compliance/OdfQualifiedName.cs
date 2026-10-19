using System;

namespace OdfKit.Compliance;

/// <summary>
/// Provides the OdfQualifiedName API.
/// 識別命名空間限定的 ODF 名稱，而不依賴 XML 前綴。
/// </summary>
public sealed class OdfQualifiedName : IEquatable<OdfQualifiedName>
{
    /// <summary>
    /// Initializes an XML name, including names without a namespace.
    /// 初始化限定名稱的新執行個體。
    /// </summary>
    /// <param name="namespaceUri">Namespace URI, or an empty string for no namespace. / 命名空間 URI；空字串表示沒有命名空間。</param>
    /// <param name="localName">Local element or attribute name. / 區域元素或屬性名稱。</param>
    public OdfQualifiedName(string namespaceUri, string localName)
    {
        if (namespaceUri == null || (namespaceUri.Length != 0 && string.IsNullOrWhiteSpace(namespaceUri)))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfQualifiedName_NamespaceCannotBeEmpty"), nameof(namespaceUri));
        if (string.IsNullOrWhiteSpace(localName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfQualifiedName_LocalCannotBeEmpty"), nameof(localName));
        NamespaceUri = namespaceUri;
        LocalName = localName;
    }

    /// <summary>
    /// Gets the NamespaceUri value.
    /// 取得命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// Gets the LocalName value.
    /// 取得區域元素或屬性名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// Performs equals.
    /// 執行 Equals。
    /// </summary>
    /// <inheritdoc />
    public bool Equals(OdfQualifiedName? other)
    {
        return other is not null &&
            string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
            string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Performs equals.
    /// 執行 Equals。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as OdfQualifiedName);

    /// <summary>
    /// Gets hash code.
    /// 取得 Hash Code。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(NamespaceUri) * 397) ^
                StringComparer.Ordinal.GetHashCode(LocalName);
        }
    }

    /// <summary>
    /// Converts to string.
    /// 轉換為 String。
    /// </summary>
    /// <inheritdoc />
    public override string ToString() => "{" + NamespaceUri + "}" + LocalName;
}

