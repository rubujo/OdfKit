using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;
/// <summary>
/// Provides the OdfPackage API.
/// 提供 OdfPackage API。
/// </summary>

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void InitializeLoad() => OdfPackageLoader.Initialize(this);

    private Task InitializeLoadAsync(CancellationToken cancellationToken = default) =>
        OdfPackageLoader.InitializeAsync(this, cancellationToken);

    #endregion
}
