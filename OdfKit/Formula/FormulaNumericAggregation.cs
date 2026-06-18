using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OdfKit.Formula;

/// <summary>
/// 數值聚合運算的向量化快速路徑（PERF-5d）。
/// </summary>
internal static class FormulaNumericAggregation
{
    /// <summary>
    /// 純數值矩陣掃描結果。
    /// </summary>
    internal readonly struct NumericMatrixScan
    {
        internal NumericMatrixScan(double sum, int count, bool success)
        {
            Sum = sum;
            Count = count;
            Success = success;
        }

        internal double Sum { get; }
        internal int Count { get; }
        internal bool Success { get; }
    }

    /// <summary>
    /// 以 SIMD 加速連續 <see cref="double"/> 陣列的總和；不足一個向量寬度時退回純量累加。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Sum(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty)
        {
            return 0;
        }

        int index = 0;
        int length = values.Length;
        double scalarSum = 0;
#if !NETSTANDARD2_0
        if (Vector.IsHardwareAccelerated && length >= Vector<double>.Count)
        {
            var sumVector = Vector<double>.Zero;
            int vectorLimit = length - (length % Vector<double>.Count);
            for (; index < vectorLimit; index += Vector<double>.Count)
            {
                sumVector += new Vector<double>(values.Slice(index, Vector<double>.Count));
            }

            for (int lane = 0; lane < Vector<double>.Count; lane++)
            {
                scalarSum += sumVector[lane];
            }
        }
#endif

        for (; index < length; index++)
        {
            scalarSum += values[index];
        }

        return scalarSum;
    }

    /// <summary>
    /// 嘗試將二維陣列展平為連續 <see cref="double"/> 緩衝區並計算總和。
    /// </summary>
    internal static bool TrySumMatrix(object[,] matrix, out double sum)
    {
        if (!TryScanNumericMatrix(matrix, out NumericMatrixScan scan))
        {
            sum = 0;
            return false;
        }

        sum = scan.Sum;
        return true;
    }

    /// <summary>
    /// 嘗試對可完全轉為 <see cref="double"/> 的二維陣列計算總和與有效計數。
    /// </summary>
    internal static bool TryScanNumericMatrix(object[,] matrix, out NumericMatrixScan scan)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        int total = rows * cols;
        if (total == 0)
        {
            scan = new NumericMatrixScan(0, 0, true);
            return true;
        }

        double[] buffer = ArrayPool<double>.Shared.Rent(total);
        try
        {
            int offset = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object? cell = matrix[r, c];
                    if (cell is null)
                    {
                        scan = default;
                        return false;
                    }

                    if (!FormulaCoercion.TryCoerceDouble(cell, out double value))
                    {
                        scan = default;
                        return false;
                    }

                    buffer[offset++] = value;
                }
            }

            double sum = Sum(buffer.AsSpan(0, total));
            scan = new NumericMatrixScan(sum, total, true);
            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 當條件欄與加總欄皆為純數值矩陣且條件為等號時，以配置池化緩衝區加速 SUMIF。
    /// </summary>
    internal static bool TrySumIfNumericEqual(
        object[,] criteriaRange,
        object[,] sumRange,
        double criterion,
        out double sum)
    {
        sum = 0;
        int rows = criteriaRange.GetLength(0);
        int cols = criteriaRange.GetLength(1);
        if (rows != sumRange.GetLength(0) || cols != sumRange.GetLength(1))
        {
            return false;
        }

        int total = rows * cols;
        if (total == 0)
        {
            return true;
        }

        double[] criteriaBuffer = ArrayPool<double>.Shared.Rent(total);
        double[] sumBuffer = ArrayPool<double>.Shared.Rent(total);
        try
        {
            int offset = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object? criteriaCell = criteriaRange[r, c];
                    object? sumCell = sumRange[r, c];
                    if (criteriaCell is null || sumCell is null)
                    {
                        return false;
                    }

                    if (!FormulaCoercion.TryCoerceDouble(criteriaCell, out double criteriaValue) ||
                        !FormulaCoercion.TryCoerceDouble(sumCell, out double sumValue))
                    {
                        return false;
                    }

                    criteriaBuffer[offset] = criteriaValue;
                    sumBuffer[offset] = sumValue;
                    offset++;
                }
            }

            for (int i = 0; i < total; i++)
            {
                if (criteriaBuffer[i] == criterion)
                {
                    sum += sumBuffer[i];
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(criteriaBuffer);
            ArrayPool<double>.Shared.Return(sumBuffer);
        }
    }

    /// <summary>
    /// 當範圍為純數值矩陣且條件為等號時，加速 COUNTIF 計數。
    /// </summary>
    internal static bool TryCountIfNumericEqual(object[,] range, double criterion, out int count)
    {
        count = 0;
        int rows = range.GetLength(0);
        int cols = range.GetLength(1);
        int total = rows * cols;
        if (total == 0)
        {
            return true;
        }

        double[] buffer = ArrayPool<double>.Shared.Rent(total);
        try
        {
            int offset = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object? cell = range[r, c];
                    if (cell is null || !FormulaCoercion.TryCoerceDouble(cell, out double value))
                    {
                        return false;
                    }

                    buffer[offset++] = value;
                }
            }

            for (int i = 0; i < total; i++)
            {
                if (buffer[i] == criterion)
                {
                    count++;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(buffer);
        }
    }
}
