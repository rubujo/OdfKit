using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 矩陣內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaMatrixFunctionHandlers
{
    internal static object EvaluateTranspose(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        object val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        if (val is object[,] arr)
        {
            int rows = arr.GetLength(0);
            int cols = arr.GetLength(1);
            var result = new object[cols, rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                    result[c, r] = arr[r, c];
            }

            return result;
        }

        var scalar = new object[1, 1];
        scalar[0, 0] = val;
        return scalar;
    }

    internal static object EvaluateMDeterm(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetMatrix(arguments, context, out double[,]? matrix, out object error))
            return error;
        if (matrix.GetLength(0) != matrix.GetLength(1))
            return OdfFormulaError.Value;

        double[,] working = (double[,])matrix.Clone();
        double determinant = 1;
        int size = working.GetLength(0);
        for (int pivotIndex = 0; pivotIndex < size; pivotIndex++)
        {
            int pivotRow = FindPivotRow(working, pivotIndex, pivotIndex);
            if (Math.Abs(working[pivotRow, pivotIndex]) <= double.Epsilon)
                return 0d;
            if (pivotRow != pivotIndex)
            {
                SwapRows(working, pivotRow, pivotIndex);
                determinant = -determinant;
            }

            double pivot = working[pivotIndex, pivotIndex];
            determinant *= pivot;
            for (int rowIndex = pivotIndex + 1; rowIndex < size; rowIndex++)
            {
                double factor = working[rowIndex, pivotIndex] / pivot;
                for (int columnIndex = pivotIndex + 1; columnIndex < size; columnIndex++)
                {
                    working[rowIndex, columnIndex] -= factor * working[pivotIndex, columnIndex];
                }
            }
        }

        return determinant;
    }

    internal static object EvaluateMInverse(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetMatrix(arguments, context, out double[,]? matrix, out object error))
            return error;
        int size = matrix.GetLength(0);
        if (size != matrix.GetLength(1))
            return OdfFormulaError.Value;

        var augmented = new double[size, size * 2];
        for (int rowIndex = 0; rowIndex < size; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < size; columnIndex++)
            {
                augmented[rowIndex, columnIndex] = matrix[rowIndex, columnIndex];
            }

            augmented[rowIndex, size + rowIndex] = 1d;
        }

        for (int pivotIndex = 0; pivotIndex < size; pivotIndex++)
        {
            int pivotRow = FindPivotRow(augmented, pivotIndex, pivotIndex);
            double pivot = augmented[pivotRow, pivotIndex];
            if (Math.Abs(pivot) <= double.Epsilon)
                return OdfFormulaError.Num;
            if (pivotRow != pivotIndex)
                SwapRows(augmented, pivotRow, pivotIndex);

            pivot = augmented[pivotIndex, pivotIndex];
            for (int columnIndex = 0; columnIndex < size * 2; columnIndex++)
            {
                augmented[pivotIndex, columnIndex] /= pivot;
            }

            for (int rowIndex = 0; rowIndex < size; rowIndex++)
            {
                if (rowIndex == pivotIndex)
                    continue;
                double factor = augmented[rowIndex, pivotIndex];
                for (int columnIndex = 0; columnIndex < size * 2; columnIndex++)
                {
                    augmented[rowIndex, columnIndex] -= factor * augmented[pivotIndex, columnIndex];
                }
            }
        }

        var result = new object[size, size];
        for (int rowIndex = 0; rowIndex < size; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < size; columnIndex++)
            {
                result[rowIndex, columnIndex] = augmented[rowIndex, size + columnIndex];
            }
        }

        return result;
    }

    internal static object EvaluateMMult(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetMatrix(arguments[0].Evaluate(context), out double[,]? left, out object leftError))
            return leftError;
        if (!TryGetMatrix(arguments[1].Evaluate(context), out double[,]? right, out object rightError))
            return rightError;
        if (left.GetLength(1) != right.GetLength(0))
            return OdfFormulaError.Value;

        var result = new object[left.GetLength(0), right.GetLength(1)];
        for (int rowIndex = 0; rowIndex < left.GetLength(0); rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < right.GetLength(1); columnIndex++)
            {
                double sum = 0;
                for (int innerIndex = 0; innerIndex < left.GetLength(1); innerIndex++)
                {
                    sum += left[rowIndex, innerIndex] * right[innerIndex, columnIndex];
                }

                result[rowIndex, columnIndex] = sum;
            }
        }

        return result;
    }

    internal static object EvaluateMUnit(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError error)
            return error;
        if (!FormulaCoercion.TryCoerceDouble(value, out double sizeValue) ||
            sizeValue < 1 ||
            sizeValue > int.MaxValue ||
            Math.Truncate(sizeValue) != sizeValue)
        {
            return OdfFormulaError.Value;
        }

        int size = (int)sizeValue;
        if (context is OdfDomEvaluationContext domContext)
        {
            domContext.Budget?.EnsureArrayResultCapacity(
                checked((long)size * size));
        }

        var result = new object[size, size];
        for (int rowIndex = 0; rowIndex < size; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < size; columnIndex++)
            {
                result[rowIndex, columnIndex] = rowIndex == columnIndex ? 1d : 0d;
            }
        }

        return result;
    }

    private static bool TryGetMatrix(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double[,] matrix,
        out object error)
    {
        if (arguments.Count != 1)
        {
            matrix = new double[0, 0];
            error = OdfFormulaError.Value;
            return false;
        }

        return TryGetMatrix(arguments[0].Evaluate(context), out matrix, out error);
    }

    private static bool TryGetMatrix(object value, out double[,] matrix, out object error)
    {
        if (value is OdfFormulaError formulaError)
        {
            matrix = new double[0, 0];
            error = formulaError;
            return false;
        }
        if (value is not object[,] source || source.Length == 0)
        {
            matrix = new double[0, 0];
            error = OdfFormulaError.Value;
            return false;
        }

        matrix = new double[source.GetLength(0), source.GetLength(1)];
        for (int rowIndex = 0; rowIndex < source.GetLength(0); rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < source.GetLength(1); columnIndex++)
            {
                if (!FormulaCoercion.TryCoerceDouble(source[rowIndex, columnIndex], out double number))
                {
                    error = OdfFormulaError.Value;
                    return false;
                }

                matrix[rowIndex, columnIndex] = number;
            }
        }

        error = 0d;
        return true;
    }

    private static int FindPivotRow(double[,] matrix, int columnIndex, int startRow)
    {
        int pivotRow = startRow;
        double pivotMagnitude = Math.Abs(matrix[startRow, columnIndex]);
        for (int rowIndex = startRow + 1; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            double candidateMagnitude = Math.Abs(matrix[rowIndex, columnIndex]);
            if (candidateMagnitude > pivotMagnitude)
            {
                pivotMagnitude = candidateMagnitude;
                pivotRow = rowIndex;
            }
        }

        return pivotRow;
    }

    private static void SwapRows(double[,] matrix, int firstRow, int secondRow)
    {
        for (int columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
        {
            double temporary = matrix[firstRow, columnIndex];
            matrix[firstRow, columnIndex] = matrix[secondRow, columnIndex];
            matrix[secondRow, columnIndex] = temporary;
        }
    }
}

