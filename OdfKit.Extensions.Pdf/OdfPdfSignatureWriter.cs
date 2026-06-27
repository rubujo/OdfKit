using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using OdfKit.Compliance;

namespace OdfKit.Export;

internal static class OdfPdfSignatureWriter
{
    private const int ContentsHexLength = 65536;
    private const string ByteRangePlaceholder = "/ByteRange [0000000000 0000000000 0000000000 0000000000]";

    internal static void Sign(Stream unsignedPdfStream, Stream destination, X509Certificate2 certificate)
    {
        if (unsignedPdfStream is null)
            throw new ArgumentNullException(nameof(unsignedPdfStream));
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate));

        byte[] originalPdf = ReadAllBytes(unsignedPdfStream);
        string originalText = Encoding.ASCII.GetString(originalPdf);
        int previousXrefOffset = FindPreviousXrefOffset(originalText);
        (int rootObjectNumber, int rootGeneration) = FindRootReference(originalText);
        int nextObjectNumber = FindNextObjectNumber(originalText);

        int signatureObjectNumber = nextObjectNumber++;
        int fieldObjectNumber = nextObjectNumber++;
        int acroFormObjectNumber = nextObjectNumber++;

        string signatureObject = CreateSignatureObject(signatureObjectNumber, certificate);
        string fieldObject = CreateFieldObject(fieldObjectNumber, signatureObjectNumber);
        string acroFormObject = CreateAcroFormObject(acroFormObjectNumber, fieldObjectNumber);
        string catalogObject = CreateUpdatedCatalogObject(
            originalText,
            rootObjectNumber,
            rootGeneration,
            acroFormObjectNumber);

        byte[] incrementalUpdate = BuildIncrementalUpdate(
            originalPdf.Length,
            previousXrefOffset,
            nextObjectNumber,
            (rootObjectNumber, catalogObject),
            (signatureObjectNumber, signatureObject),
            (fieldObjectNumber, fieldObject),
            (acroFormObjectNumber, acroFormObject));

        byte[] unsignedSignedPdf = Concat(originalPdf, incrementalUpdate);
        ApplyByteRange(unsignedSignedPdf, out int contentsStart, out int contentsEnd);

        byte[] signedContent = BuildSignedContent(unsignedSignedPdf, contentsStart, contentsEnd);
        byte[] signature = CreateCmsSignature(signedContent, certificate);
        if (signature.Length * 2 > ContentsHexLength)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        string signatureHex = ToUpperHex(signature).PadRight(ContentsHexLength, '0');
        byte[] signatureHexBytes = Encoding.ASCII.GetBytes(signatureHex);
        Buffer.BlockCopy(signatureHexBytes, 0, unsignedSignedPdf, contentsStart + 1, signatureHexBytes.Length);
        destination.Write(unsignedSignedPdf, 0, unsignedSignedPdf.Length);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static int FindPreviousXrefOffset(string pdfText)
    {
        int marker = pdfText.LastIndexOf("startxref", StringComparison.Ordinal);
        if (marker < 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        Match match = Regex.Match(pdfText.Substring(marker), @"startxref\s+(?<offset>\d+)");
        return match.Success && int.TryParse(match.Groups["offset"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset)
            ? offset
            : throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
    }

    private static (int ObjectNumber, int Generation) FindRootReference(string pdfText)
    {
        Match match = Regex.Match(pdfText, @"/Root\s+(?<obj>\d+)\s+(?<gen>\d+)\s+R", RegexOptions.RightToLeft);
        if (!match.Success ||
            !int.TryParse(match.Groups["obj"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectNumber) ||
            !int.TryParse(match.Groups["gen"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int generation))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        return (objectNumber, generation);
    }

    private static int FindNextObjectNumber(string pdfText)
    {
        int max = 0;
        foreach (Match match in Regex.Matches(pdfText, @"(?m)^\s*(?<obj>\d+)\s+\d+\s+obj\b"))
        {
            if (int.TryParse(match.Groups["obj"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectNumber))
            {
                max = Math.Max(max, objectNumber);
            }
        }

        return max + 1;
    }

    private static string CreateSignatureObject(int objectNumber, X509Certificate2 certificate)
    {
        string name = EscapePdfString(certificate.GetNameInfo(X509NameType.SimpleName, false));
        string timestamp = DateTimeOffset.UtcNow.ToString("'D:'yyyyMMddHHmmss'+00''00'", CultureInfo.InvariantCulture);
        string contents = new('0', ContentsHexLength);
        return $"""
            {objectNumber} 0 obj
            << /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached
               {ByteRangePlaceholder}
               /Contents <{contents}>
               /Name ({name}) /M ({timestamp}) /Reason (OdfKit PDF export signature) >>
            endobj
            """;
    }

    private static string CreateFieldObject(int objectNumber, int signatureObjectNumber) =>
        $"""
        {objectNumber} 0 obj
        << /Type /Annot /Subtype /Widget /FT /Sig /T (OdfKitSignature1) /F 132 /Rect [0 0 0 0] /V {signatureObjectNumber} 0 R >>
        endobj
        """;

    private static string CreateAcroFormObject(int objectNumber, int fieldObjectNumber) =>
        $"""
        {objectNumber} 0 obj
        << /Fields [{fieldObjectNumber} 0 R] /SigFlags 3 >>
        endobj
        """;

    private static string CreateUpdatedCatalogObject(
        string pdfText,
        int rootObjectNumber,
        int rootGeneration,
        int acroFormObjectNumber)
    {
        Match match = Regex.Match(
            pdfText,
            $@"(?s){rootObjectNumber}\s+{rootGeneration}\s+obj\s*(?<body><<.*?>>)\s*endobj");
        if (!match.Success)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        string body = match.Groups["body"].Value;
        if (Regex.IsMatch(body, @"/AcroForm\s+\d+\s+\d+\s+R"))
        {
            body = Regex.Replace(body, @"/AcroForm\s+\d+\s+\d+\s+R", $"/AcroForm {acroFormObjectNumber} 0 R");
        }
        else
        {
            int insertAt = body.LastIndexOf(">>", StringComparison.Ordinal);
            if (insertAt < 0)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
            }

            body = body.Insert(insertAt, $" /AcroForm {acroFormObjectNumber} 0 R");
        }

        return $"""
            {rootObjectNumber} {rootGeneration} obj
            {body}
            endobj
            """;
    }

    private static byte[] BuildIncrementalUpdate(
        int originalLength,
        int previousXrefOffset,
        int size,
        params (int ObjectNumber, string Body)[] objects)
    {
        using var stream = new MemoryStream();
        var offsets = new (int ObjectNumber, int Offset)[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            offsets[i] = (objects[i].ObjectNumber, originalLength + checked((int)stream.Position));
            byte[] objectBytes = Encoding.ASCII.GetBytes(objects[i].Body.Replace("\r\n", "\n"));
            stream.Write(objectBytes, 0, objectBytes.Length);
            stream.WriteByte((byte)'\n');
        }

        long xrefOffset = originalLength + stream.Position;
        WriteAscii(stream, "xref\n");
        foreach ((int objectNumber, int offset) in offsets.OrderBy(static item => item.ObjectNumber))
        {
            WriteAscii(stream, FormattableString.Invariant($"{objectNumber} 1\n{offset:0000000000} 00000 n \n"));
        }

        WriteAscii(stream, "trailer\n");
        WriteAscii(stream, FormattableString.Invariant($"<< /Size {size} /Root {objects[0].ObjectNumber} 0 R /Prev {previousXrefOffset} >>\n"));
        WriteAscii(stream, "startxref\n");
        WriteAscii(stream, FormattableString.Invariant($"{xrefOffset}\n%%EOF\n"));
        return stream.ToArray();
    }

    private static void ApplyByteRange(byte[] pdf, out int contentsStart, out int contentsEnd)
    {
        byte[] contentsMarker = Encoding.ASCII.GetBytes("/Contents <");
        int markerIndex = LastIndexOf(pdf, contentsMarker);
        if (markerIndex < 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        contentsStart = markerIndex + contentsMarker.Length - 1;
        contentsEnd = contentsStart + ContentsHexLength + 2;
        string replacement = FormattableString.Invariant(
            $"/ByteRange [{0:0000000000} {contentsStart:0000000000} {contentsEnd:0000000000} {pdf.Length - contentsEnd:0000000000}]");
        ReplaceAscii(pdf, ByteRangePlaceholder, replacement);
    }

    private static byte[] BuildSignedContent(byte[] pdf, int contentsStart, int contentsEnd)
    {
        using var stream = new MemoryStream(pdf.Length - (contentsEnd - contentsStart));
        stream.Write(pdf, 0, contentsStart);
        stream.Write(pdf, contentsEnd, pdf.Length - contentsEnd);
        return stream.ToArray();
    }

    private static byte[] CreateCmsSignature(byte[] content, X509Certificate2 certificate)
    {
        var contentInfo = new ContentInfo(content);
        var signedCms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signedCms.ComputeSignature(signer, silent: true);
        return signedCms.Encode();
    }

    private static string EscapePdfString(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

    private static string ToUpperHex(byte[] bytes)
    {
        char[] chars = new char[bytes.Length * 2];
        const string digits = "0123456789ABCDEF";
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = digits[bytes[i] >> 4];
            chars[(i * 2) + 1] = digits[bytes[i] & 0x0F];
        }

        return new string(chars);
    }

    private static byte[] Concat(byte[] first, byte[] second)
    {
        byte[] result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static void WriteAscii(Stream stream, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = haystack.Length - needle.Length; i >= 0; i--)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static void ReplaceAscii(byte[] bytes, string placeholder, string replacement)
    {
        if (placeholder.Length != replacement.Length)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        byte[] placeholderBytes = Encoding.ASCII.GetBytes(placeholder);
        int index = LastIndexOf(bytes, placeholderBytes);
        if (index < 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        byte[] replacementBytes = Encoding.ASCII.GetBytes(replacement);
        Buffer.BlockCopy(replacementBytes, 0, bytes, index, replacementBytes.Length);
    }
}
