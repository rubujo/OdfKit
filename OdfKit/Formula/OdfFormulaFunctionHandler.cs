using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// Evaluates an application-defined spreadsheet function.
/// 評估應用程式定義的試算表函式。
/// </summary>
/// <param name="arguments">The eagerly evaluated argument values. / 已依序完成評估的引數值。</param>
/// <param name="context">The spreadsheet evaluation context. / 試算表評估內容模型。</param>
/// <returns>The function result or an <see cref="OdfFormulaError"/>. / 函式結果或 <see cref="OdfFormulaError"/>。</returns>
public delegate object OdfFormulaFunctionHandler(IReadOnlyList<object> arguments, IEvaluationContext context);
