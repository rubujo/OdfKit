namespace OdfKit.Core;
/// <summary>
/// Provides the OdfPackage API.
/// 提供 OdfPackage API。
/// </summary>

public sealed partial class OdfPackage
{
    // 儲存掛鉤與簽章清除已遷移至 OdfPackageSaveHooksEngine、OdfPackageSignaturePurgeEngine。

    private void RemoveOutdatedSignatures()
        => OdfPackageSignaturePurgeEngine.RemoveOutdatedSignatures(_entries, _manifest);
}
