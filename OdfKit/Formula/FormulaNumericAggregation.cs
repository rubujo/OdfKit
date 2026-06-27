using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
#if NET10_0_OR_GREATER
using System.Numerics.Tensors;
#endif

namespace OdfKit.Formula;

/// <summary>
/// 數值聚合運算的向量化快速路徑（PERF-5d）。
/// </summary>
internal static class FormulaNumericAggregation
{
    internal static bool LastSumProductUsedVectorizedPathForTests { get; private set; }

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
    internal static unsafe double Sum(ReadOnlySpan<double> values)
    {
#if NET10_0_OR_GREATER
        return TensorPrimitives.Sum(values);
#else
        if (values.IsEmpty)
        {
            return 0;
        }

        int index = 0;
        int length = values.Length;
        double scalarSum = 0;

        if (Vector.IsHardwareAccelerated && length >= Vector<double>.Count)
        {
            var sumVector = Vector<double>.Zero;
            int vectorLimit = length - (length % Vector<double>.Count);
            fixed (double* p = values)
            {
                for (; index < vectorLimit; index += Vector<double>.Count)
                {
                    sumVector += Unsafe.Read<Vector<double>>(p + index);
                }
            }

            for (int lane = 0; lane < Vector<double>.Count; lane++)
            {
                scalarSum += sumVector[lane];
            }
        }

        for (; index < length; index++)
        {
            scalarSum += values[index];
        }

        return scalarSum;
#endif
    }

    /// <summary>
    /// 以 SIMD 加速連續 <see cref="double"/> 陣列的最小值；不足一個向量寬度時退回純量比較。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe double Min(ReadOnlySpan<double> values)
    {
#if NET10_0_OR_GREATER
        return TensorPrimitives.Min(values);
#else
        if (values.IsEmpty)
        {
            return double.MaxValue;
        }

        int index = 0;
        int length = values.Length;
        double scalarMin = double.MaxValue;

        if (Vector.IsHardwareAccelerated && length >= Vector<double>.Count)
        {
            var minVector = new Vector<double>(double.MaxValue);
            int vectorLimit = length - (length % Vector<double>.Count);
            fixed (double* p = values)
            {
                for (; index < vectorLimit; index += Vector<double>.Count)
                {
                    var vec = Unsafe.Read<Vector<double>>(p + index);
                    minVector = Vector.Min(minVector, vec);
                }
            }

            for (int lane = 0; lane < Vector<double>.Count; lane++)
            {
                if (minVector[lane] < scalarMin)
                {
                    scalarMin = minVector[lane];
                }
            }
        }

        for (; index < length; index++)
        {
            if (values[index] < scalarMin)
            {
                scalarMin = values[index];
            }
        }

        return scalarMin;
#endif
    }

    /// <summary>
    /// 以 SIMD 加速連續 <see cref="double"/> 陣列的最大值；不足一個向量寬度時退回純量比較。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe double Max(ReadOnlySpan<double> values)
    {
#if NET10_0_OR_GREATER
        return TensorPrimitives.Max(values);
#else
        if (values.IsEmpty)
        {
            return double.MinValue;
        }

        int index = 0;
        int length = values.Length;
        double scalarMax = double.MinValue;

        if (Vector.IsHardwareAccelerated && length >= Vector<double>.Count)
        {
            var maxVector = new Vector<double>(double.MinValue);
            int vectorLimit = length - (length % Vector<double>.Count);
            fixed (double* p = values)
            {
                for (; index < vectorLimit; index += Vector<double>.Count)
                {
                    var vec = Unsafe.Read<Vector<double>>(p + index);
                    maxVector = Vector.Max(maxVector, vec);
                }
            }

            for (int lane = 0; lane < Vector<double>.Count; lane++)
            {
                if (maxVector[lane] > scalarMax)
                {
                    scalarMax = maxVector[lane];
                }
            }
        }

        for (; index < length; index++)
        {
            if (values[index] > scalarMax)
            {
                scalarMax = values[index];
            }
        }

        return scalarMax;
#endif
    }

    /// <summary>
    /// 嘗試對二維陣列計算最大值。
    /// </summary>
    internal static bool TryMaxMatrix(object[,] matrix, out double max, out bool hasNumber)
    {
        max = double.MinValue;
        hasNumber = false;
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
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
                    object? cell = matrix[r, c];
                    if (cell is null)
                    {
                        return false;
                    }

                    if (!FormulaCoercion.TryCoerceDouble(cell, out double value))
                    {
                        return false;
                    }

                    buffer[offset++] = value;
                }
            }

            if (offset > 0)
            {
                max = Max(buffer.AsSpan(0, offset));
                hasNumber = true;
            }
            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 嘗試對二維陣列計算最小值。
    /// </summary>
    internal static bool TryMinMatrix(object[,] matrix, out double min, out bool hasNumber)
    {
        min = double.MaxValue;
        hasNumber = false;
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
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
                    object? cell = matrix[r, c];
                    if (cell is null)
                    {
                        return false;
                    }

                    if (!FormulaCoercion.TryCoerceDouble(cell, out double value))
                    {
                        return false;
                    }

                    buffer[offset++] = value;
                }
            }

            if (offset > 0)
            {
                min = Min(buffer.AsSpan(0, offset));
                hasNumber = true;
            }
            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(buffer);
        }
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
    /// 當兩個範圍皆為純數值矩陣時，以連續緩衝區與 SIMD / Tensor dot product 加速 SUMPRODUCT。
    /// </summary>
    internal static unsafe bool TrySumProduct(object[,] left, object[,] right, out double sum)
    {
        LastSumProductUsedVectorizedPathForTests = false;
        sum = 0;

        int rows = left.GetLength(0);
        int cols = left.GetLength(1);
        if (rows != right.GetLength(0) || cols != right.GetLength(1))
        {
            return false;
        }

        int total = rows * cols;
        if (total == 0)
        {
            return true;
        }

        double[] leftBuffer = ArrayPool<double>.Shared.Rent(total);
        double[] rightBuffer = ArrayPool<double>.Shared.Rent(total);
        try
        {
            int offset = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object? leftCell = left[r, c];
                    object? rightCell = right[r, c];
                    if (leftCell is null || rightCell is null)
                    {
                        return false;
                    }

                    if (!FormulaCoercion.TryCoerceDouble(leftCell, out double leftValue) ||
                        !FormulaCoercion.TryCoerceDouble(rightCell, out double rightValue))
                    {
                        return false;
                    }

                    leftBuffer[offset] = leftValue;
                    rightBuffer[offset] = rightValue;
                    offset++;
                }
            }

            ReadOnlySpan<double> leftSpan = leftBuffer.AsSpan(0, total);
            ReadOnlySpan<double> rightSpan = rightBuffer.AsSpan(0, total);
#if NET10_0_OR_GREATER
            sum = TensorPrimitives.Dot(leftSpan, rightSpan);
            LastSumProductUsedVectorizedPathForTests = true;
#else
            int i = 0;
            if (Vector.IsHardwareAccelerated && total >= Vector<double>.Count)
            {
                var sumVector = Vector<double>.Zero;
                int vectorLimit = total - (total % Vector<double>.Count);
                fixed (double* pLeft = leftBuffer)
                fixed (double* pRight = rightBuffer)
                {
                    for (; i < vectorLimit; i += Vector<double>.Count)
                    {
                        var leftVec = Unsafe.Read<Vector<double>>(pLeft + i);
                        var rightVec = Unsafe.Read<Vector<double>>(pRight + i);
                        sumVector += leftVec * rightVec;
                    }
                }

                for (int lane = 0; lane < Vector<double>.Count; lane++)
                {
                    sum += sumVector[lane];
                }

                LastSumProductUsedVectorizedPathForTests = true;
            }

            for (; i < total; i++)
            {
                sum += leftSpan[i] * rightSpan[i];
            }
#endif
            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(leftBuffer);
            ArrayPool<double>.Shared.Return(rightBuffer);
        }
    }

    /// <summary>
    /// 當條件欄與加總欄皆為純數值矩陣且條件為等號時，以配置池化緩衝區加速 SUMIF。
    /// </summary>
    internal static unsafe bool TrySumIfNumericEqual(
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

            int i = 0;
            if (Vector.IsHardwareAccelerated && total >= Vector<double>.Count)
            {
                var criterionVec = new Vector<double>(criterion);
                var sumVec = Vector<double>.Zero;
                int vectorLimit = total - (total % Vector<double>.Count);

                fixed (double* pCriteria = criteriaBuffer)
                fixed (double* pSum = sumBuffer)
                {
                    for (; i < vectorLimit; i += Vector<double>.Count)
                    {
                        var critVal = Unsafe.Read<Vector<double>>(pCriteria + i);
                        var sumVal = Unsafe.Read<Vector<double>>(pSum + i);

                        var mask = Vector.Equals(critVal, criterionVec);
                        var matchedSum = Vector.ConditionalSelect(mask, sumVal, Vector<double>.Zero);
                        sumVec += matchedSum;
                    }
                }

                for (int lane = 0; lane < Vector<double>.Count; lane++)
                {
                    sum += sumVec[lane];
                }
            }

            for (; i < total; i++)
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
    internal static unsafe bool TryCountIfNumericEqual(object[,] range, double criterion, out int count)
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

            int i = 0;
            if (Vector.IsHardwareAccelerated && total >= Vector<double>.Count)
            {
                var criterionVec = new Vector<double>(criterion);
                var matchCountVec = Vector<double>.Zero;
                var oneVec = new Vector<double>(1.0);
                int vectorLimit = total - (total % Vector<double>.Count);

                fixed (double* pBuffer = buffer)
                {
                    for (; i < vectorLimit; i += Vector<double>.Count)
                    {
                        var val = Unsafe.Read<Vector<double>>(pBuffer + i);
                        var mask = Vector.Equals(val, criterionVec);
                        var matchOne = Vector.ConditionalSelect(mask, oneVec, Vector<double>.Zero);
                        matchCountVec += matchOne;
                    }
                }

                for (int lane = 0; lane < Vector<double>.Count; lane++)
                {
                    count += (int)matchCountVec[lane];
                }
            }

            for (; i < total; i++)
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
