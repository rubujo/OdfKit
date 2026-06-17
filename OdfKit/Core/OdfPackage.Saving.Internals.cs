namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    // 儲存掛鉤與簽章清除已遷移至 OdfPackageSaveHooksEngine、OdfPackageSignaturePurgeEngine。

    private void RemoveOutdatedSignatures()
        => OdfPackageSignaturePurgeEngine.RemoveOutdatedSignatures(_entries, _manifest);
}
