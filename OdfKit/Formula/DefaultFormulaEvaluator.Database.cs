using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Database Functions

    private static object EvaluateDatabaseFunction(string name, List<AstNode> arguments, IEvaluationContext context, Func<List<double>, object> aggregator)
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

        // Resolve field column index
        int fieldCol = -1;
        if (TryCoerceDouble(fieldVal, out double colD))
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

        // Map criteria columns
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

        var selectedValues = new List<double>();

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
                object cellVal = db[r, fieldCol];
                if (TryCoerceDouble(cellVal, out double val))
                {
                    selectedValues.Add(val);
                }
            }
        }

        return aggregator(selectedValues);
    }

    private static object EvaluateDSum(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DSUM", arguments, context, list =>
        {
            double sum = 0;
            foreach (var d in list)
                sum += d;
            return sum;
        });
    }

    private static object EvaluateDAverage(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DAVERAGE", arguments, context, list =>
        {
            if (list.Count == 0)
                return OdfFormulaError.Div0;
            double sum = 0;
            foreach (var d in list)
                sum += d;
            return sum / list.Count;
        });
    }

    private static object EvaluateDCount(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DCOUNT", arguments, context, list => (double)list.Count);
    }

    private static object EvaluateDMax(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMAX", arguments, context, list =>
        {
            if (list.Count == 0)
                return 0.0;
            double max = double.MinValue;
            foreach (var d in list)
                if (d > max)
                    max = d;
            return max;
        });
    }

    private static object EvaluateDMin(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMIN", arguments, context, list =>
        {
            if (list.Count == 0)
                return 0.0;
            double min = double.MaxValue;
            foreach (var d in list)
                if (d < min)
                    min = d;
            return min;
        });
    }

    #endregion
}
