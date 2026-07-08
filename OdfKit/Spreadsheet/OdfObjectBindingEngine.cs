using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

internal static class OdfObjectBindingEngine
{
    internal static IReadOnlyList<OdfObjectColumn> GetReadableColumns<T>(OdfObjectBindingOptions options)
    {
        List<OdfObjectColumn> columns = GetProperties<T>()
            .Where(property => property.CanRead)
            .Select(property => CreateColumn(property, options, options.ColumnMap))
            .Where(column => !column.Ignore)
            .OrderBy(column => column.Order ?? int.MaxValue)
            .ThenBy(column => column.MetadataToken)
            .ToList();

        if (columns.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_ObjectDataReader_NoReadableProperties", typeof(T).Name));
        }

        return columns;
    }

    internal static IReadOnlyList<OdfObjectColumn> GetWritableColumns<T>(OdfObjectReadOptions options)
    {
        List<OdfObjectColumn> columns = GetProperties<T>()
            .Where(property => property.CanWrite)
            .Select(property => CreateColumn(property, null, options.ColumnMap))
            .Where(column => !column.Ignore)
            .OrderBy(column => column.Order ?? int.MaxValue)
            .ThenBy(column => column.MetadataToken)
            .ToList();

        if (columns.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_ObjectDataReader_NoReadableProperties", typeof(T).Name));
        }

        return columns;
    }

    internal static object? NormalizeWriteValue(object? value, OdfObjectBindingOptions options)
    {
        if (value is null)
        {
            return options.NullValuePolicy == OdfObjectNullValuePolicy.EmptyString ? string.Empty : null;
        }

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            Guid guid => guid.ToString("D"),
            Enum enumValue => enumValue.ToString(),
            _ => value
        };
    }

    internal static object? ConvertReadValue(object? value, Type targetType, CultureInfo culture)
    {
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        string text = value as string ?? Convert.ToString(value, culture) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        if (effectiveType == typeof(string))
        {
            return text;
        }

        if (effectiveType == typeof(Guid))
        {
            return Guid.Parse(text);
        }

        if (effectiveType == typeof(DateTime))
        {
            return value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(text, culture, DateTimeStyles.RoundtripKind);
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(text, culture, DateTimeStyles.RoundtripKind);
        }

        if (effectiveType.IsEnum)
        {
            return Enum.Parse(effectiveType, text, ignoreCase: true);
        }

        if (effectiveType == typeof(bool) && (text == "1" || text.Equals("yes", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (effectiveType == typeof(bool) && (text == "0" || text.Equals("no", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return Convert.ChangeType(value is string ? text : value, effectiveType, culture);
    }

    private static IEnumerable<PropertyInfo> GetProperties<T>() =>
        typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken);

    private static OdfObjectColumn CreateColumn(
        PropertyInfo property,
        OdfObjectBindingOptions? options,
        OdfObjectColumnMap? columnMap)
    {
        OdfObjectColumnMapping? mapping = columnMap?.Find(property.Name);
        string header = mapping?.Header
            ?? ResolveDefaultHeader(property);
        header = options?.HeaderNameSelector?.Invoke(header) ?? header;

        return new OdfObjectColumn(
            property,
            header,
            mapping?.Aliases.ToArray() ?? Array.Empty<string>(),
            mapping?.Format,
            mapping?.Order,
            mapping?.Ignore == true,
            property.MetadataToken,
            mapping?.RequiredColumn == true,
            mapping?.RequiredValue == true,
            mapping?.DefaultValue,
            mapping?.DefaultValueFactory);
    }

    private static string ResolveDefaultHeader(PropertyInfo property) =>
        property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
        ?? TryGetDisplayAttributeName(property)
        ?? property.Name;

    private static string? TryGetDisplayAttributeName(PropertyInfo property)
    {
        Attribute? displayAttribute = property.GetCustomAttributes()
            .FirstOrDefault(attribute => attribute.GetType().FullName == "System.ComponentModel.DataAnnotations.DisplayAttribute");
        if (displayAttribute is null)
        {
            return null;
        }

        MethodInfo? getName = displayAttribute.GetType().GetMethod("GetName", Type.EmptyTypes);
        return getName?.Invoke(displayAttribute, null) as string;
    }
}

internal readonly struct OdfObjectColumn(
    PropertyInfo property,
    string header,
    IReadOnlyList<string> aliases,
    OdfObjectColumnFormat? format,
    int? order,
    bool ignore,
    int metadataToken,
    bool requiredColumn,
    bool requiredValue,
    object? defaultValue,
    Func<object?>? defaultValueFactory)
{
    internal PropertyInfo Property { get; } = property;

    internal string Header { get; } = header;

    internal IReadOnlyList<string> Aliases { get; } = aliases;

    internal OdfObjectColumnFormat? Format { get; } = format;

    internal int? Order { get; } = order;

    internal bool Ignore { get; } = ignore;

    internal int MetadataToken { get; } = metadataToken;

    internal bool RequiredColumn { get; } = requiredColumn;

    internal bool RequiredValue { get; } = requiredValue;

    internal object? DefaultValue { get; } = defaultValue;

    internal Func<object?>? DefaultValueFactory { get; } = defaultValueFactory;
}
