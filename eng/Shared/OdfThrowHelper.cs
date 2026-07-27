using System;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace OdfKit.Internal;

/// <summary>
/// Provides cross-target guard helpers for OdfKit and its extension assemblies.
/// 提供 OdfKit 與其擴充組件使用的跨目標防護協助程式。
/// </summary>
internal static class OdfThrowHelper
{
    /// <summary>
    /// Throws when a string argument is null or empty.
    /// 當字串引數為 null 或空字串時擲出例外。
    /// </summary>
    public static void ThrowIfNullOrEmpty(string? value, string parameterName)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
#else
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException(null, parameterName);
        }
#endif
    }

    /// <summary>
    /// Throws when the owning object has been disposed.
    /// 當擁有者物件已釋放時擲出例外。
    /// </summary>
    /// <param name="condition">Whether the object has been disposed. / 物件是否已釋放。</param>
    /// <param name="objectName">The disposed object name. / 已釋放物件名稱。</param>
    public static void ThrowIfDisposed(bool condition, string objectName)
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(condition, objectName);
#else
        if (condition)
        {
            throw new ObjectDisposedException(objectName);
        }
#endif
    }

    /// <summary>
    /// Throws when the supplied argument is null.
    /// 當指定引數為 null 時擲出例外。
    /// </summary>
    /// <param name="value">The value to validate. / 要驗證的值。</param>
    /// <param name="parameterName">The parameter name. / 參數名稱。</param>
    public static void ThrowIfNull(
#if NET6_0_OR_GREATER
        [NotNull]
#endif
        object? value,
        string parameterName)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
    }

    /// <summary>
    /// Throws when a value is less than its lower bound.
    /// 當值小於下限時擲出例外。
    /// </summary>
    public static void ThrowIfLessThan<T>(T value, T lowerBound, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// Throws when a value is negative.
    /// 當值為負數時擲出例外。
    /// </summary>
    public static void ThrowIfNegative<T>(T value, string parameterName)
    {
        if (Comparer<T>.Default.Compare(value, default!) < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// Throws when a value is negative or zero.
    /// 當值為負數或零時擲出例外。
    /// </summary>
    public static void ThrowIfNegativeOrZero<T>(T value, string parameterName)
    {
        if (Comparer<T>.Default.Compare(value, default!) <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// Throws when a value is greater than its upper bound.
    /// 當值大於上限時擲出例外。
    /// </summary>
    public static void ThrowIfGreaterThan<T>(T value, T upperBound, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) > 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// Throws when a value is greater than or equal to its exclusive upper bound.
    /// 當值大於或等於排他上限時擲出例外。
    /// </summary>
    public static void ThrowIfGreaterThanOrEqual<T>(T value, T upperBound, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) >= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
