using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// 表示公式分析結果。
/// </summary>
public sealed class OdfFormulaAnalysis
{
    /// <summary>
    /// 初始化 <see cref="OdfFormulaAnalysis"/> 類別的新執行個體。
    /// </summary>
    /// <param name="originalFormula">原始公式</param>
    /// <param name="normalizedFormula">供剖析使用的標準化公式</param>
    /// <param name="serializedFormula">重新序列化的公式，若無法安全序列化則為 null</param>
    /// <param name="functions">公式中出現的函式名稱</param>
    /// <param name="diagnostics">診斷清單</param>
    public OdfFormulaAnalysis(
        string originalFormula,
        string normalizedFormula,
        string? serializedFormula,
        IReadOnlyList<string> functions,
        IReadOnlyList<OdfFormulaDiagnostic> diagnostics)
    {
        OriginalFormula = originalFormula;
        NormalizedFormula = normalizedFormula;
        SerializedFormula = serializedFormula;
        Functions = functions;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 取得原始公式。
    /// </summary>
    public string OriginalFormula { get; }

    /// <summary>
    /// 取得供剖析使用的標準化公式。
    /// </summary>
    public string NormalizedFormula { get; }

    /// <summary>
    /// 取得重新序列化的公式，若無法安全序列化則為 null。
    /// </summary>
    public string? SerializedFormula { get; }

    /// <summary>
    /// 取得公式中出現的函式名稱。
    /// </summary>
    public IReadOnlyList<string> Functions { get; }

    /// <summary>
    /// 取得診斷清單。
    /// </summary>
    public IReadOnlyList<OdfFormulaDiagnostic> Diagnostics { get; }

    /// <summary>
    /// 取得一個值，指出公式可被剖析且沒有錯誤診斷。
    /// </summary>
    public bool CanParse
    {
        get
        {
            foreach (var diagnostic in Diagnostics)
            {
                if (diagnostic.Severity == OdfFormulaDiagnosticSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 取得一個值，指出公式包含預設評估器不支援的函式。
    /// </summary>
    public bool HasUnsupportedFunctions
    {
        get
        {
            foreach (var diagnostic in Diagnostics)
            {
                if (diagnostic.Code == OdfFormulaSupport.UnsupportedFunctionCode)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
