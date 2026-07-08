namespace OdfKit;

/// <summary>
/// Represents one unresolved template placeholder with a lightweight location hint.
/// 表示一個未解析模板占位符與其輕量位置提示。
/// </summary>
/// <param name="Expression">The placeholder expression without braces. / 不含大括號的占位符運算式。</param>
/// <param name="DocumentKind">The practical document kind. / 實務文件種類。</param>
/// <param name="LocationHint">The lightweight location hint. / 輕量位置提示。</param>
public sealed record OdfTemplateUnresolvedPlaceholder(
    string Expression,
    string DocumentKind,
    string LocationHint);
