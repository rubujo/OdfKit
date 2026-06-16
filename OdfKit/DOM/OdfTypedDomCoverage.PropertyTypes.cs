using System;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.DOM;

public static partial class OdfTypedDomCoverage
{
    #region Property Type Resolution

    private static string GetPropertyTypeName(Type type)
    {
        Type? nullableType = Nullable.GetUnderlyingType(type);
        Type resolvedType = nullableType ?? type;
        if (resolvedType.IsGenericType &&
            resolvedType.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
            typeof(OdfElement).IsAssignableFrom(resolvedType.GetGenericArguments()[0]))
        {
            return "childElementCollection";
        }

        return TryResolvePrimitivePropertyTypeName(resolvedType)
            ?? TryResolveExtendedPropertyTypeName(resolvedType)
            ?? resolvedType.FullName ?? resolvedType.Name;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => version.ToString()
        };
    }

    #endregion
}
