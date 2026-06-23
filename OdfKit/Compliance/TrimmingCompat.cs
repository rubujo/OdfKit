#if NETSTANDARD2_0
using System;

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// 支援 netstandard2.0 編譯 trimming 與 Native AOT 屬性之內部相容性 shim 類別。
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
                AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method,
                Inherited = false)]
internal sealed class DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) : Attribute
{
    /// <summary>
    /// 取得宣告需要動態存取的成員類型。
    /// </summary>
    public DynamicallyAccessedMemberTypes MemberTypes { get; } = memberTypes;
}

/// <summary>
/// 支援 netstandard2.0 編譯之動態存取成員類型列舉值。
/// </summary>
[Flags]
internal enum DynamicallyAccessedMemberTypes
{
    /// <summary>
    /// 無。
    /// </summary>
    None = 0,

    /// <summary>
    /// 公用無參數建構函式。
    /// </summary>
    PublicParameterlessConstructor = 0x0001,

    /// <summary>
    /// 所有公用建構函式。
    /// </summary>
    PublicConstructors = 0x0002 | PublicParameterlessConstructor,

    /// <summary>
    /// 所有非公用建構函式。
    /// </summary>
    NonPublicConstructors = 0x0004,

    /// <summary>
    /// 所有公用方法。
    /// </summary>
    PublicMethods = 0x0008,

    /// <summary>
    /// 所有非公用方法。
    /// </summary>
    NonPublicMethods = 0x0010,

    /// <summary>
    /// 所有公用欄位。
    /// </summary>
    PublicFields = 0x0020,

    /// <summary>
    /// 所有非公用欄位。
    /// </summary>
    NonPublicFields = 0x0040,

    /// <summary>
    /// 所有公用巢狀類型。
    /// </summary>
    PublicNestedTypes = 0x0080,

    /// <summary>
    /// 所有非公用巢狀類型。
    /// </summary>
    NonPublicNestedTypes = 0x0100,

    /// <summary>
    /// 所有公用屬性。
    /// </summary>
    PublicProperties = 0x0200,

    /// <summary>
    /// 所有非公用屬性。
    /// </summary>
    NonPublicProperties = 0x0400,

    /// <summary>
    /// 所有公用事件。
    /// </summary>
    PublicEvents = 0x0800,

    /// <summary>
    /// 所有非公用事件。
    /// </summary>
    NonPublicEvents = 0x1000,

    /// <summary>
    /// 所有成員。
    /// </summary>
    All = -1
}

/// <summary>
/// 支援 netstandard2.0 編譯之 RequiresUnreferencedCode 屬性。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
internal sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute
{
    /// <summary>
    /// 取得說明為何該成員與 trimming 不相容的訊息。
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// 取得或設定該警告的詳細說明 URL。
    /// </summary>
    public string? Url { get; set; }
}

/// <summary>
/// 支援 netstandard2.0 編譯之 RequiresDynamicCode 屬性。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
internal sealed class RequiresDynamicCodeAttribute(string message) : Attribute
{
    /// <summary>
    /// 取得說明為何該成員需要執行期動態程式碼產生的訊息。
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// 取得或設定該警告的詳細說明 URL。
    /// </summary>
    public string? Url { get; set; }
}
#endif
