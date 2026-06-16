namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void InitializeLoad() => OdfPackageLoader.Initialize(this);

    #endregion
}
