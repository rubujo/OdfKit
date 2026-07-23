using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 提供 ODF 封裝中繼資料所使用的官方版本化結構描述。
/// </summary>
internal static class OdfPackageSchemaRegistry
{
    internal static OdfSchemaSet GetManifestSchema(OdfVersion version) => version switch
    {
        OdfVersion.Odf10 => Odf10ManifestSchemaMetadata.Create(),
        OdfVersion.Odf11 => Odf11ManifestSchemaMetadata.Create(),
        OdfVersion.Odf12 => Odf12ManifestSchemaMetadata.Create(),
        OdfVersion.Odf13 => Odf13ManifestSchemaMetadata.Create(),
        OdfVersion.Odf14 => Odf14ManifestSchemaMetadata.Create(),
        _ => Odf14ManifestSchemaMetadata.Create()
    };

    internal static OdfSchemaSet? GetDigitalSignatureSchema(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => Odf12DsigSchemaMetadata.Create(),
        OdfVersion.Odf13 => Odf13DsigSchemaMetadata.Create(),
        OdfVersion.Odf14 => Odf14DsigSchemaMetadata.Create(),
        _ => null
    };
}
