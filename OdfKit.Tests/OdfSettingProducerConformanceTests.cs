using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests that supported setting producers retain their declared numeric datatype domains.
/// 測試支援的設定產生器維持宣告的數值型別範圍。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSettingProducerConformanceTests
{
    /// <summary>
    /// Zoom values outside the serialized integer domain fail before changing the document.
    /// 超出序列化整數範圍的縮放值在修改文件前拒絕。
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(2147483648d)]
    [InlineData(-2147483649d)]
    public void ZoomRejectsValuesOutsideItsDeclaredDatatype(double value)
    {
        foreach (OdfVersion version in new[] { OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
        {
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            Assert.Throws<ArgumentOutOfRangeException>(() => document.ZoomLevel = value);
            Assert.Equal(100d, document.ZoomLevel);
            AssertValid(document, version);
        }
    }

    /// <summary>
    /// Link update modes outside the serialized short domain fail without replacing the default.
    /// 超出序列化短整數範圍的連結更新模式拒絕寫入且不取代缺省值。
    /// </summary>
    [Theory]
    [InlineData(-32769)]
    [InlineData(32768)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void LinkModesRejectValuesOutsideTheirDeclaredDatatype(int value)
    {
        foreach (OdfVersion version in new[] { OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
        {
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            Assert.Throws<ArgumentOutOfRangeException>(() => document.LinkUpdateMode = value);
            Assert.Equal(2, document.LinkUpdateMode);
            AssertValid(document, version);
        }
    }

    /// <summary>
    /// Existing settings adopt the datatype required by the producer that replaces their value.
    /// 既有設定在產生器取代值時採用該產生器要求的型別。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void UpdatingSettingsRestoresTheirDeclaredProducerTypes(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        document.ZoomLevel = 125;
        document.LinkUpdateMode = 1;
        foreach (OdfNode item in document.SettingsDom.Descendants().Where(node => node.NamespaceUri == OdfNamespaces.Config && node.LocalName == "config-item" && node.GetAttribute("name", OdfNamespaces.Config) is "ZoomValue" or "LinkUpdateMode"))
            item.SetAttribute("type", OdfNamespaces.Config, "boolean", "config");
        document.ZoomLevel = 150;
        document.LinkUpdateMode = 2;
        Assert.Equal("int", Setting(document, "ZoomValue").GetAttribute("type", OdfNamespaces.Config));
        Assert.Equal("short", Setting(document, "LinkUpdateMode").GetAttribute("type", OdfNamespaces.Config));
        AssertValid(document, version);
    }

    /// <summary>
    /// Integer boundaries and the existing rounding policy produce conforming settings.
    /// 整數邊界與既有取整規則產生合規設定。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ValidNumericBoundariesKeepTheirProducerBehavior(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        foreach (double zoom in new[] { int.MinValue, int.MaxValue, -2147483648.49, 2147483647.49, 124.5, 125.5 })
        {
            document.ZoomLevel = zoom;
            Assert.Equal(Math.Round(zoom), document.ZoomLevel);
            AssertValid(document, version);
        }
        foreach (int mode in new[] { short.MinValue, short.MaxValue, 0, 1, 2 })
        {
            document.LinkUpdateMode = mode;
            Assert.Equal(mode, document.LinkUpdateMode);
            AssertValid(document, version);
        }
    }

    /// <summary>
    /// Stored XML integers use invariant syntax independently of the current culture's negative sign.
    /// 保存的 XML 整數使用不受目前文化特性負號影響的固定語法。
    /// </summary>
    [Fact]
    public void StoredIntegerSettingsIgnoreTheCurrentCulture()
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ar-SA");
            using TextDocument document = TextDocument.Create();
            document.LinkUpdateMode = short.MinValue;
            document.ZoomLevel = 125;
            Assert.Equal(short.MinValue, document.LinkUpdateMode);
            Assert.Equal(125d, document.ZoomLevel);
            AssertValid(document, OdfVersion.Odf14);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static OdfNode Setting(TextDocument document, string name)
        => document.SettingsDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Config && node.LocalName == "config-item" && node.GetAttribute("name", OdfNamespaces.Config) == name);

    private static void AssertValid(TextDocument document, OdfVersion version)
    {
        using MemoryStream output = new();
        document.SaveToStream(output);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
