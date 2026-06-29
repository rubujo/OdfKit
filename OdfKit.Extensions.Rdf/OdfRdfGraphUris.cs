namespace OdfKit.Extensions.Rdf;

/// <summary>
/// Defines URI helpers used by the ODF RDF graph bridge.
/// 提供 OdfKit RDF 圖形橋接使用的 URI 慣例。
/// </summary>
public static class OdfRdfGraphUris
{
    /// <summary>
    /// Gets the default base URI for package RDF subjects.
    /// 預設封裝基底 URI；空白文件主詞會對應至此 URI。
    /// </summary>
    public static Uri DefaultPackageBaseUri { get; } = new("urn:odfkit:odf-package:/");

    /// <summary>
    /// Resolves a package-relative RDF subject URI against the base URI.
    /// 將 OdfKit 主詞字串解析為絕對 URI。
    /// </summary>
    /// <param name="subject">The value to use. / OdfKit 主詞 IRI 或相對路徑</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 可用於 dotNetRDF 的絕對 URI</returns>
    public static Uri ResolveSubjectUri(string subject, Uri? baseUri = null)
    {
        Uri graphBase = baseUri ?? DefaultPackageBaseUri;
        if (string.IsNullOrEmpty(subject))
        {
            return graphBase;
        }

        if (Uri.TryCreate(subject, UriKind.Absolute, out Uri? absolute))
        {
            return absolute;
        }

        return new Uri(graphBase, subject);
    }

    /// <summary>
    /// Converts a URI into a package RDF subject string.
    /// 將 dotNetRDF 節點 URI 還原為 OdfKit 主詞字串。
    /// </summary>
    /// <param name="uri">The path or URI. / 節點 URI</param>
    /// <param name="baseUri">The path or URI. / 封裝基底 URI</param>
    /// <returns>The result. / OdfKit 主詞字串</returns>
    public static string ToSubjectString(Uri uri, Uri? baseUri = null)
    {
        Uri graphBase = baseUri ?? DefaultPackageBaseUri;
        if (uri.Equals(graphBase))
        {
            return string.Empty;
        }

        if (graphBase.IsAbsoluteUri && uri.IsAbsoluteUri && graphBase.Scheme == uri.Scheme && graphBase.Authority == uri.Authority)
        {
            string basePath = graphBase.AbsolutePath;
            string uriPath = uri.AbsolutePath;
            if (uriPath.StartsWith(basePath, StringComparison.Ordinal))
            {
                string relative = uriPath.Substring(basePath.Length);
                if (relative.StartsWith("/", StringComparison.Ordinal))
                {
                    relative = relative.Substring(1);
                }

                if (!string.IsNullOrEmpty(relative))
                {
                    return relative;
                }
            }
        }

        return uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString();
    }
}
