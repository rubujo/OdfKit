using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    private static readonly string[] XmlEntries =
    [
        "content.xml",
        "styles.xml",
        "meta.xml",
        "settings.xml"
    ];
    public static void ValidatePackage(
        OdfPackage package,
        OdfComplianceProfile? profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        if (profile is null)
        {
            return;
        }

        ValidateMacroEntries(package, profile, issues);
        ValidatePackageSchemaPatterns(package, profile, schema, issues);

        foreach (string entryName in GetProfileXmlEntries(package))
        {
            if (!package.HasEntry(entryName))
            {
                continue;
            }

            try
            {
                using Stream stream = package.GetEntryStream(entryName);
                ScanXml(stream, entryName, profile, schema, issues, closeInput: true);
            }
            catch (IOException ex)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0302",
                    $"ODF XML entry cannot be read for profile checks: {ex.Message}",
                    entryName,
                    profileId: profile.Id));
            }
            catch (SecurityException ex)
            {
                issues.Add(new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0303",
                    $"ODF XML entry failed security validation during profile checks: {ex.Message}",
                    entryName,
                    profileId: profile.Id));
            }
        }
    }

    public static void ValidateFlatXml(
        Stream stream,
        string? fileName,
        OdfComplianceProfile? profile,
        OdfSchemaSet schema,
        List<OdfValidationIssue> issues)
    {
        if (profile is null || !stream.CanSeek)
        {
            return;
        }

        long originalPosition = stream.Position;
        stream.Position = 0;

        try
        {
            ScanXml(stream, fileName, profile, schema, issues, closeInput: false);
            stream.Position = 0;
            ValidateFlatSchemaPattern(stream, fileName, profile, schema, issues);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
