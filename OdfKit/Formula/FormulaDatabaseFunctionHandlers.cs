using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 資料庫內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaDatabaseFunctionHandlers
{


    internal static object EvaluateDatabaseFunction(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<List<object>, object> aggregator)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;

        var dbVal = arguments[0].Evaluate(context);
        var fieldVal = arguments[1].Evaluate(context);
        var criteriaVal = arguments[2].Evaluate(context);

        if (dbVal is OdfFormulaError err1)
            return err1;
        if (fieldVal is OdfFormulaError err2)
            return err2;
        if (criteriaVal is OdfFormulaError err3)
            return err3;

        if (dbVal is not object[,] db || criteriaVal is not object[,] crit)
            return OdfFormulaError.Value;

        int dbRows = db.GetLength(0);
        int dbCols = db.GetLength(1);
        int critRows = crit.GetLength(0);
        int critCols = crit.GetLength(1);

        if (dbRows < 2 || dbCols < 1 || critRows < 2 || critCols < 1)
            return OdfFormulaError.Value;

        // 解析欄位資料行索引
        int fieldCol = -1;
        if (FormulaCoercion.TryCoerceDouble(fieldVal, out double colD))
        {
            fieldCol = (int)colD - 1;
        }
        else
        {
            string fieldStr = fieldVal.ToString() ?? "";
            for (int c = 0; c < dbCols; c++)
            {
                if (string.Equals(db[0, c]?.ToString(), fieldStr, StringComparison.OrdinalIgnoreCase))
                {
                    fieldCol = c;
                    break;
                }
            }
        }

        if (fieldCol < 0 || fieldCol >= dbCols)
            return OdfFormulaError.Value;

        // 對應條件資料行
        var critColMap = new Dictionary<int, int>(); // critCol -> dbCol
        for (int c = 0; c < critCols; c++)
        {
            string header = crit[0, c]?.ToString() ?? "";
            if (string.IsNullOrEmpty(header))
                continue;

            int mappedCol = -1;
            for (int dc = 0; dc < dbCols; dc++)
            {
                if (string.Equals(db[0, dc]?.ToString(), header, StringComparison.OrdinalIgnoreCase))
                {
                    mappedCol = dc;
                    break;
                }
            }
            critColMap[c] = mappedCol;
        }

        var selectedValues = new List<object>();

        // 針對資料庫中的每一列（不含標頭列）
        for (int r = 1; r < dbRows; r++)
        {
            bool rowMatches = false;

            // 比對條件列（不含標頭列）
            // 若有任一條件列符合，則 rowMatches = true（各列之間為 OR 邏輯）
            for (int cr = 1; cr < critRows; cr++)
            {
                bool critRowMatches = true;
                bool hasConditions = false;

                // 條件列中的所有條件均必須符合（同一列的各欄之間為 AND 邏輯）
                for (int cc = 0; cc < critCols; cc++)
                {
                    object critCell = crit[cr, cc];
                    if (critCell == null || string.IsNullOrEmpty(critCell.ToString()))
                        continue;

                    hasConditions = true;
                    int dbCol = critColMap.TryGetValue(cc, out int mapped) ? mapped : -1;
                    if (dbCol < 0)
                    {
                        critRowMatches = false;
                        break;
                    }

                    object dbCell = db[r, dbCol];
                    var matcher = new CriteriaMatcher(critCell);
                    if (!matcher.Matches(dbCell))
                    {
                        critRowMatches = false;
                        break;
                    }
                }

                if (hasConditions && critRowMatches)
                {
                    rowMatches = true;
                    break;
                }
            }

            if (rowMatches)
            {
                selectedValues.Add(db[r, fieldCol]);
            }
        }

        return aggregator(selectedValues);
    }

    internal static object EvaluateDSum(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DSUM", arguments, context, list =>
        {
            double sum = 0;
            foreach (object value in list)
            {
                if (FormulaCoercion.TryCoerceDouble(value, out double number))
                    sum += number;
            }
            return sum;
        });
    }

    internal static object EvaluateDAverage(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DAVERAGE", arguments, context, list =>
        {
            List<double> numbers = GetNumbers(list);
            if (numbers.Count == 0)
                return OdfFormulaError.Div0;
            double sum = 0;
            foreach (double number in numbers)
                sum += number;
            return sum / numbers.Count;
        });
    }

    internal static object EvaluateDCount(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DCOUNT", arguments, context, list => (double)GetNumbers(list).Count);
    }

    internal static object EvaluateDMax(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMAX", arguments, context, list =>
        {
            List<double> numbers = GetNumbers(list);
            if (numbers.Count == 0)
                return 0.0;
            double max = double.MinValue;
            foreach (double number in numbers)
                if (number > max)
                    max = number;
            return max;
        });
    }

    internal static object EvaluateDMin(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMIN", arguments, context, list =>
        {
            List<double> numbers = GetNumbers(list);
            if (numbers.Count == 0)
                return 0.0;
            double min = double.MaxValue;
            foreach (double number in numbers)
                if (number < min)
                    min = number;
            return min;
        });
    }

    internal static object EvaluateDCountA(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateDatabaseFunction("DCOUNTA", arguments, context, list =>
        {
            int count = 0;
            foreach (object value in list)
            {
                if (value is not null && (value is not string text || text.Length > 0))
                    count++;
            }

            return (double)count;
        });

    internal static object EvaluateDGet(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateDatabaseFunction("DGET", arguments, context, list => list.Count switch
        {
            1 => list[0],
            0 => OdfFormulaError.Value,
            _ => OdfFormulaError.Num
        });

    internal static object EvaluateDProduct(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateDatabaseFunction("DPRODUCT", arguments, context, list =>
        {
            double product = 1;
            foreach (double number in GetNumbers(list))
                product *= number;
            return product;
        });

    internal static object EvaluateDStDev(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateVariance(arguments, context, squareRoot: true, sample: true);

    internal static object EvaluateDStDevP(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateVariance(arguments, context, squareRoot: true, sample: false);

    internal static object EvaluateDVar(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateVariance(arguments, context, squareRoot: false, sample: true);

    internal static object EvaluateDVarP(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateVariance(arguments, context, squareRoot: false, sample: false);

    private static object EvaluateVariance(
        List<AstNode> arguments,
        IEvaluationContext context,
        bool squareRoot,
        bool sample)
        => EvaluateDatabaseFunction("DVAR", arguments, context, list =>
        {
            List<double> numbers = GetNumbers(list);
            int divisor = sample ? numbers.Count - 1 : numbers.Count;
            if (divisor <= 0)
                return OdfFormulaError.Div0;

            double sum = 0;
            foreach (double number in numbers)
                sum += number;
            double average = sum / numbers.Count;
            double squaredDifferences = 0;
            foreach (double number in numbers)
            {
                double difference = number - average;
                squaredDifferences += difference * difference;
            }

            double variance = squaredDifferences / divisor;
            return squareRoot ? Math.Sqrt(variance) : variance;
        });

    private static List<double> GetNumbers(List<object> values)
    {
        var numbers = new List<double>(values.Count);
        foreach (object value in values)
        {
            if (FormulaCoercion.TryCoerceDouble(value, out double number))
                numbers.Add(number);
        }

        return numbers;
    }
}

