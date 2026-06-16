using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 內建公式函式名稱至評估處理常式的註冊表。
/// </summary>
internal delegate object FormulaBuiltinHandler(List<AstNode> arguments, IEvaluationContext context);

/// <summary>
/// 內建公式函式分派註冊表（取代巨型 switch）。
/// </summary>
internal static class FormulaBuiltinFunctionRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<string, FormulaBuiltinHandler>> s_registry =
        new(DefaultFormulaEvaluator.CreateBuiltinRegistry);

    /// <summary>
    /// 依函式名稱分派至已註冊的內建處理常式。
    /// </summary>
    internal static object Evaluate(string name, List<AstNode> arguments, IEvaluationContext context)
    {
        try
        {
            if (s_registry.Value.TryGetValue(name, out FormulaBuiltinHandler? handler))
                return handler(arguments, context);

            return OdfFormulaError.Name;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OdfKitDiagnostics.Warn($"Formula function '{name}' threw unexpected exception: {ex.GetType().Name}");
            return OdfFormulaError.Value;
        }
    }
}
