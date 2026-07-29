namespace OdfKit;

/// <summary>
/// Resolves one template value path segment without runtime reflection.
/// 在不使用執行期反射的情況下解析一個範本值路徑片段。
/// </summary>
public interface IOdfTemplateValueResolver
{
    /// <summary>
    /// Tries to resolve a named value from the supplied source object.
    /// 嘗試從指定來源物件解析具名值。
    /// </summary>
    /// <param name="source">The source object. / 來源物件。</param>
    /// <param name="name">The path segment name. / 路徑片段名稱。</param>
    /// <param name="value">The resolved value when found. / 找到時的解析值。</param>
    /// <returns><see langword="true"/> when the value was resolved; otherwise, <see langword="false"/>. / 成功解析值時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    bool TryResolve(object source, string name, out object? value);
}
