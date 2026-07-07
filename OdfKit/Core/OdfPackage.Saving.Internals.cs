namespace OdfKit.Core;
/// <summary>
/// Adds internal save helpers for ODF package serialization.
/// 提供 ODF 封裝序列化使用的內部儲存輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    // 儲存掛鉤與簽章清除已遷移至 OdfPackageSaveHooksEngine、OdfPackageSignaturePurgeEngine。

    private void RemoveOutdatedSignatures()
        => OdfPackageSignaturePurgeEngine.RemoveOutdatedSignatures(_entries, _manifest);
}
