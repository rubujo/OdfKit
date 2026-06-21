using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 在完整結構描述驗證可用之前，執行套件層級的 ODF 驗證。
/// </summary>
public static partial class OdfPackageValidator
{
    private static readonly Dictionary<string, string> ExpectedRootNames = new(StringComparer.Ordinal)
    {
        ["content.xml"] = "document-content",
        ["styles.xml"] = "document-styles",
        ["meta.xml"] = "document-meta",
        ["settings.xml"] = "document-settings"
    };

    private static readonly Dictionary<string, string> ExpectedManifestMediaTypes = new(StringComparer.Ordinal)
    {
        ["content.xml"] = "text/xml",
        ["styles.xml"] = "text/xml",
        ["meta.xml"] = "text/xml",
        ["settings.xml"] = "text/xml",
        ["META-INF/documentsignatures.xml"] = "text/xml"
    };
    /// <summary>
    /// 驗證 ODF 套件是否符合套件層級規則與選用的設定檔。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="profile">相容性設定檔</param>
    /// <param name="fileName">檔案名稱</param>
    /// <param name="culture">指定此問題生成時使用的文化特性，若為 null 則自動偵測</param>
    /// <returns>驗證結果報告</returns>
    public static OdfValidationReport Validate(
        OdfPackage package,
        OdfComplianceProfile? profile = null,
        string? fileName = null,
        System.Globalization.CultureInfo? culture = null)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));

        List<OdfValidationIssue> issues = [];

        // 智慧偵測語系
        System.Globalization.CultureInfo targetCulture = culture
            ?? profile?.TargetCulture
            ?? System.Globalization.CultureInfo.CurrentUICulture;

        string? profileId = profile?.Id;
        string? mimeType = package.MimeType;
        OdfDocumentKind mimeKind = OdfDocumentKindDetector.FromMimeType(mimeType);
        OdfDocumentKind extensionKind = OdfDocumentKindDetector.FromFileName(fileName);
        OdfDocumentKind bodyKind = DetectBodyKind(package, issues, profileId);
        OdfDocumentKind documentKind = mimeKind != OdfDocumentKind.Unknown ? mimeKind : bodyKind;

        ValidateMimeType(mimeType, mimeKind, profile, profileId, issues);
        ValidateMimeTypeEntry(package, profileId, issues);
        ValidateBodyKind(mimeKind, bodyKind, profileId, issues);
        ValidateExtensionKind(extensionKind, mimeKind, fileName, profileId, issues);
        ValidateEntryPaths(package, profileId, issues);
        ValidateManifest(package, mimeType, profileId, issues);

        OdfVersion detectedVersion = DetectVersion(package, issues, profileId);
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(detectedVersion);
        if (!OdfSchemaRegistry.HasNativeSchema(detectedVersion) && detectedVersion != OdfVersion.Unknown)
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Warning,
                "ODF1005",
                $"The validator is using ODF 1.4 schema to perform best-effort validation on ODF {OdfVersionInfo.ToVersionString(detectedVersion)} document.",
                fileName,
                profileId: profileId));
        }
        ValidateXmlRoots(package, schema, issues, profileId);
        ValidateProfileExtension(fileName, profile, profileId, issues);
        ValidateProfileVersion(detectedVersion, profile, profileId, issues, fileName, "/office:document-content[1]");
        OdfProfileRuleValidator.ValidatePackage(package, profile, schema, issues);

        foreach (var issue in issues)
        {
            issue.Culture ??= targetCulture;
        }

        return new OdfValidationReport(detectedVersion, documentKind, issues);
    }

    #region Profile Extension & Version

    private static void ValidateProfileExtension(
        string? fileName,
        OdfComplianceProfile? profile,
        string? profileId,
        List<OdfValidationIssue> issues)
    {
        if (profile is null || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        if (!profile.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1002",
                $"File extension '{extension}' is not allowed by profile '{profile.Id}'.",
                fileName,
                profileId: profileId));
        }
    }

    private static void ValidateProfileVersion(
        OdfVersion detectedVersion,
        OdfComplianceProfile? profile,
        string? profileId,
        List<OdfValidationIssue> issues,
        string? fileName = null,
        string? xPath = null)
    {
        if (profile is null || detectedVersion == OdfVersion.Unknown)
        {
            return;
        }

        if (!profile.SupportedVersions.Contains(detectedVersion))
        {
            issues.Add(new OdfValidationIssue(
                OdfIssueSeverity.Error,
                "ODF1001",
                $"ODF version '{detectedVersion}' is not allowed by profile '{profile.Id}'.",
                packagePath: fileName,
                xPath: xPath,
                requiredVersion: profile.SupportedVersions.Minimum,
                profileId: profileId));
        }
    }

    private sealed class XmlRootInfo(string namespaceUri, string localName, string? version)
    {
        public string NamespaceUri { get; } = namespaceUri;

        public string LocalName { get; } = localName;

        public string? Version { get; } = version;
    }

    #endregion

}
