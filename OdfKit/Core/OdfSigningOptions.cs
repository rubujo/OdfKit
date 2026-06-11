using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core
{
    /// <summary>
    /// XAdES standard levels.
    /// </summary>
    public enum XadesLevel
    {
        /// <summary>
        /// Plain W3C XMLDSig signature without XAdES extensions.
        /// </summary>
        None,

        /// <summary>
        /// XAdES Basic Electronic Signature (XAdES-BES).
        /// </summary>
        BES,

        /// <summary>
        /// XAdES with Timestamp (XAdES-T).
        /// </summary>
        T,

        /// <summary>
        /// XAdES Archive / Long Term Validation (XAdES-A).
        /// </summary>
        A
    }

    /// <summary>
    /// Supported signature levels for ODF documents.
    /// </summary>
    public enum OdfSignatureLevel
    {
        /// <summary>
        /// Plain XMLDSig signature without XAdES extensions.
        /// </summary>
        None = 0,

        /// <summary>
        /// XAdES Basic Electronic Signature (XAdES-BES).
        /// </summary>
        XadesBes = 1,

        /// <summary>
        /// XAdES with Timestamp (XAdES-T).
        /// </summary>
        XadesT = 2,

        /// <summary>
        /// XAdES Archive / Long Term Validation (XAdES-A).
        /// </summary>
        XadesA = 3
    }

    /// <summary>
    /// Configuration options for signing and verifying ODF packages with Digital Signatures / XAdES.
    /// </summary>
    public class OdfSigningOptions
    {
        /// <summary>
        /// Gets or sets the signature level.
        /// </summary>
        public OdfSignatureLevel SignatureLevel { get; set; } = OdfSignatureLevel.None;

        /// <summary>
        /// Gets or sets the XAdES standard level (None/XMLDSig, BES, T, A).
        /// </summary>
        public XadesLevel Level
        {
            get
            {
                return SignatureLevel switch
                {
                    OdfSignatureLevel.None => XadesLevel.None,
                    OdfSignatureLevel.XadesBes => XadesLevel.BES,
                    OdfSignatureLevel.XadesT => XadesLevel.T,
                    OdfSignatureLevel.XadesA => XadesLevel.A,
                    _ => XadesLevel.None
                };
            }
            set
            {
                SignatureLevel = value switch
                {
                    XadesLevel.None => OdfSignatureLevel.None,
                    XadesLevel.BES => OdfSignatureLevel.XadesBes,
                    XadesLevel.T => OdfSignatureLevel.XadesT,
                    XadesLevel.A => OdfSignatureLevel.XadesA,
                    _ => OdfSignatureLevel.None
                };
            }
        }

        /// <summary>
        /// Gets or sets the RFC 3161 Time Stamping Authority (TSA) URL.
        /// </summary>
        public string? TsaUrl { get; set; }

        /// <summary>
        /// Gets or sets whether to check certificate revocation (via CRLs).
        /// </summary>
        public bool CheckRevocation { get; set; } = false;

        /// <summary>
        /// Gets or sets a custom HttpClient to be used for fetching CRLs and querying the TSA.
        /// Useful for offline mock testing.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Gets or sets whether to allow an untrusted root certificate when validating signatures.
        /// </summary>
        public bool AllowUntrustedRoot { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to allow an untrusted timestamp certificate when validating signatures.
        /// </summary>
        public bool AllowUntrustedTimestamp { get; set; } = false;

        /// <summary>
        /// Gets additional certificates to use when building signing or validation chains.
        /// </summary>
        public X509Certificate2Collection ExtraCertificates { get; } = new X509Certificate2Collection();
    }
}
