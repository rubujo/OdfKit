using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdfKit.Text;

/// <summary>
/// Partial: reflection-free property accessor cache for streaming mail merge.
/// Partial：串流郵件合併用的免反射屬性存取快取。
/// </summary>
public static partial class OdfStreamingMailMerge
{
    #region Reflection Free Expression Trees Cache

    private static class MailMergeExpressionCache
    {
        private const int Capacity = 4096;
        private static readonly Dictionary<(Type, string), Func<object, object?>> _cache = new();
        private static readonly object _lock = new();

        public static object? GetValue(object item, string propertyName)
        {
            if (item is IDictionary<string, object?> dict)
            {
                return dict.TryGetValue(propertyName, out var val) ? val : null;
            }

            Type type = item.GetType();
            Func<object, object?>? accessor;
            lock (_lock)
            {
                if (!_cache.TryGetValue((type, propertyName), out accessor))
                {
                    PropertyInfo? prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop is not null && prop.CanRead)
                    {
#if NET10_0_OR_GREATER
                        if (!RuntimeFeature.IsDynamicCodeCompiled)
                        {
                            accessor = obj => prop.GetValue(obj);
                        }
                        else
#endif
                        {
                            ParameterExpression param = Expression.Parameter(typeof(object), "obj");
                            UnaryExpression castParam = Expression.Convert(param, type);
                            MemberExpression member = Expression.Property(castParam, prop);
                            UnaryExpression castResult = Expression.Convert(member, typeof(object));
                            accessor = Expression.Lambda<Func<object, object?>>(castResult, param).Compile();
                        }
                    }
                    else
                    {
                        accessor = static _ => null;
                    }

                    if (_cache.Count >= Capacity)
                    {
                        _cache.Clear();
                    }

                    _cache[(type, propertyName)] = accessor;
                }
            }
            return accessor(item);
        }
    }

    #endregion
}
