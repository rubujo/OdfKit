using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;
/// <summary>
/// Adds package initialization helpers used by the load pipeline.
/// 提供載入管線使用的封裝初始化輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void InitializeLoad() => OdfPackageLoader.Initialize(this);

    private Task InitializeLoadAsync(CancellationToken cancellationToken = default) =>
        OdfPackageLoader.InitializeAsync(this, cancellationToken);

    #endregion
}
