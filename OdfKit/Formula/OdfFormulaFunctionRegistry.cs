using System;
using System.Collections.Generic;
using OdfKit.Compliance;

namespace OdfKit.Formula;

/// <summary>
/// Stores application-defined formula functions for one evaluator instance.
/// 儲存單一評估器執行個體使用的應用程式自訂公式函式。
/// </summary>
/// <remarks>
/// The registry never overrides built-in functions. Instance scope prevents one tenant or document pipeline from mutating another pipeline's formula behavior.
/// 此註冊表不會覆寫內建函式；執行個體範圍可避免租戶或文件管線變更其它管線的公式行為。
/// </remarks>
public sealed class OdfFormulaFunctionRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, OdfFormulaFunctionHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a snapshot of registered function names.
    /// 取得已註冊函式名稱的快照。
    /// </summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_syncRoot)
            {
                string[] names = new string[_handlers.Count];
                _handlers.Keys.CopyTo(names, 0);
                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                return names;
            }
        }
    }

    /// <summary>
    /// Registers a new function and rejects duplicate names.
    /// 註冊新函式，並拒絕重複名稱。
    /// </summary>
    /// <param name="name">The OpenFormula-compatible function name. / 與 OpenFormula 相容的函式名稱。</param>
    /// <param name="handler">The function handler. / 函式處理常式。</param>
    public void Register(string name, OdfFormulaFunctionHandler handler)
    {
        Validate(name, handler);
        lock (_syncRoot)
        {
            if (_handlers.ContainsKey(name))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfFormulaFunctionRegistry_FunctionAlreadyRegistered", name),
                    nameof(name));
            }

            _handlers.Add(name, handler);
        }
    }

    /// <summary>
    /// Registers a function or replaces the existing application-defined handler.
    /// 註冊函式，或取代既有的應用程式自訂處理常式。
    /// </summary>
    /// <param name="name">The OpenFormula-compatible function name. / 與 OpenFormula 相容的函式名稱。</param>
    /// <param name="handler">The function handler. / 函式處理常式。</param>
    public void AddOrUpdate(string name, OdfFormulaFunctionHandler handler)
    {
        Validate(name, handler);
        lock (_syncRoot)
        {
            _handlers[name] = handler;
        }
    }

    /// <summary>
    /// Removes an application-defined function.
    /// 移除應用程式自訂函式。
    /// </summary>
    /// <param name="name">The function name. / 函式名稱。</param>
    /// <returns><see langword="true"/> when a function was removed. / 若已移除函式則為 <see langword="true"/>。</returns>
    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return _handlers.Remove(name);
        }
    }

    /// <summary>
    /// Determines whether an application-defined function is registered.
    /// 判斷是否已註冊應用程式自訂函式。
    /// </summary>
    /// <param name="name">The function name. / 函式名稱。</param>
    /// <returns><see langword="true"/> when the function is registered. / 若已註冊函式則為 <see langword="true"/>。</returns>
    public bool Contains(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return _handlers.ContainsKey(name);
        }
    }

    internal bool TryGetHandler(string name, out OdfFormulaFunctionHandler? handler)
    {
        lock (_syncRoot)
        {
            return _handlers.TryGetValue(name, out handler);
        }
    }

    private static void Validate(string name, OdfFormulaFunctionHandler handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(
                nameof(handler),
                OdfLocalizer.GetMessage("Err_OdfFormulaFunctionRegistry_HandlerNull"));
        }

        if (!IsValidFunctionName(name))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfFormulaFunctionRegistry_InvalidFunctionName", name ?? string.Empty),
                nameof(name));
        }
    }

    private static bool IsValidFunctionName(string? name)
    {
        if (name is null || string.IsNullOrWhiteSpace(name) || !(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        for (int index = 1; index < name.Length; index++)
        {
            char character = name[index];
            if (!(char.IsLetterOrDigit(character) || character is '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
