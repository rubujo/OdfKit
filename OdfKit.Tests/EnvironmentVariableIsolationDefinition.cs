using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 將會暫時修改行程環境變數的測試集中於不可平行化的集合，避免跨測試觀察到中間狀態。
/// </summary>
[CollectionDefinition("SequentialRenderingTests", DisableParallelization = true)]
public sealed class EnvironmentVariableIsolationDefinition
{
}
