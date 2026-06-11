using System;
using System.Collections.Generic;

namespace OdfKit.Core
{
    public class OdfEncryptionInfo
    {
        public string ChecksumType { get; set; } = "SHA256";
        public byte[] Checksum { get; set; } = Array.Empty<byte>();
        public string AlgorithmName { get; set; } = string.Empty;
        public byte[] InitialisationVector { get; set; } = Array.Empty<byte>();
        public string KeyDerivationName { get; set; } = "PBKDF2";
        public int KeySize { get; set; }
        public int IterationCount { get; set; }
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public string? StartKeyGenerationName { get; set; }
        public int? StartKeySize { get; set; }
        public Dictionary<string, string> ExtensionProperties { get; set; } = new(StringComparer.Ordinal);
    }
}
