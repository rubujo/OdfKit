using OdfKit.Compliance;

namespace OdfKit.WebFonts.Hosting.AspNetCore;

internal static class OdfWebFontOptionValidator
{
    public static void Validate(OdfWebFontOptions options)
    {
        if (options is null
            || string.IsNullOrWhiteSpace(options.AssetRootPath)
            || !IsApplicationRoute(options.RoutePrefix)
            || !IsPublicBaseUrl(options.PublicBaseUrl)
            || options.MaxManifestBytes <= 0
            || options.MaxAssetCount <= 0
            || options.MaxAssetBytes <= 0
            || options.AllowedOrigins.Count > 64
            || options.AllowedOrigins.Any(origin => !IsOrigin(origin)))
        {
            throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfWebFontAssetStore_ConfigurationInvalid"));
        }
    }

    public static bool IsAllowedOrigin(OdfWebFontOptions options, string origin)
        => options.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

    private static bool IsApplicationRoute(string value)
        => value.Length is > 1 and <= 256
            && value[0] == '/'
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Contains('\\')
            && !value.Contains('?')
            && !value.Contains('#');

    private static bool IsPublicBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.AbsolutePath.Contains('\\'))
        {
            return false;
        }

        string canonical = uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped).TrimEnd('/');
        return string.Equals(value.TrimEnd('/'), canonical, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return string.Equals(value.TrimEnd('/'), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }
}
