using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core
{
    /// <summary>
    /// Represents the validation results for all signatures within an ODF package.
    /// </summary>
    public class OdfSignatureValidationResult
    {
        /// <summary>
        /// Gets or sets whether all signatures in the package are valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the collection of individual signature validation results.
        /// </summary>
        public List<OdfSingleSignatureValidationResult> Signatures { get; } = new List<OdfSingleSignatureValidationResult>();
    }

    /// <summary>
    /// Represents the detailed validation result of a single digital signature.
    /// </summary>
    public class OdfSingleSignatureValidationResult
    {
        /// <summary>
        /// Gets or sets the signature identifier if available.
        /// </summary>
        public string? SignatureId { get; set; }

        /// <summary>
        /// Gets or sets whether the XML cryptographic signature is valid.
        /// </summary>
        public bool IsSignatureValid { get; set; }

        /// <summary>
        /// Gets or sets the signing certificate.
        /// </summary>
        public X509Certificate2? Certificate { get; set; }

        /// <summary>
        /// Gets or sets whether the signing certificate is valid (e.g. not expired or not yet valid).
        /// </summary>
        public bool IsCertificateValid { get; set; }

        /// <summary>
        /// Gets or sets whether the certificate chain is valid.
        /// </summary>
        public bool IsChainValid { get; set; }

        /// <summary>
        /// Gets or sets whether the XAdES-T timestamp (if present) is cryptographically valid.
        /// </summary>
        public bool IsTimestampValid { get; set; }

        /// <summary>
        /// Gets or sets whether the revocation check (CRL/OCSP) succeeded and the certificate is not revoked.
        /// </summary>
        public bool IsRevocationValid { get; set; }

        /// <summary>
        /// Gets or sets any error or warning message encountered during validation.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the diagnostic error code if validation failed.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets the warnings encountered during validation.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// Gets the ODF package references verified for this signature.
        /// </summary>
        public List<string> CheckedReferences { get; } = new List<string>();

        /// <summary>
        /// Gets the trace logs of validation steps executed.
        /// </summary>
        public List<string> ValidationSteps { get; } = new List<string>();
    }
}
