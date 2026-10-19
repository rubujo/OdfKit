using System;

namespace OdfKit.Core;

/// <summary>
/// 在同步建構期間將嵌入路徑傳給只有封裝參數的文件包裝器。
/// </summary>
internal static class OdfEmbeddedDocumentConstruction
{
    [ThreadStatic]
    private static Construction? _current;

    internal static T Create<T>(OdfPackage package, string subPath, Func<T> create)
    {
        Construction? previous = _current;
        _current = new Construction(package, subPath);
        try
        {
            return create();
        }
        finally
        {
            _current = previous;
        }
    }

    internal static string ResolveSubPath(OdfPackage package, string subPath)
    {
        if (_current is not { Claimed: false } construction || !ReferenceEquals(construction.Package, package))
            return subPath;
        // 只供同封裝的第一個 base 建構函式取用；巢狀 root 文件不繼承此路徑。
        construction.Claimed = true;
        return subPath.Length == 0 ? construction.SubPath : subPath;
    }

    private sealed class Construction(OdfPackage package, string subPath)
    {
        internal readonly OdfPackage Package = package;
        internal readonly string SubPath = subPath;
        internal bool Claimed;
    }
}
