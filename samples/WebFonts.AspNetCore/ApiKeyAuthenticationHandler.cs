using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OdfKit.WebFonts.AspNetCore.Sample;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OdfWebFontApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string supplied = Request.Headers["X-OdfKit-WebFont-Key"].ToString();
        string expected = Options.ClaimsIssuer ?? string.Empty;
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] suppliedHash = SHA256.HashData(suppliedBytes);
        byte[] expectedHash = SHA256.HashData(expectedBytes);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "odf-webfont-generator")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            SchemeName)));
    }
}
