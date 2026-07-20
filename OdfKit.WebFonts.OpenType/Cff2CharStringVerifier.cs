using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class Cff2CharStringVerifier
{
    private const int MaximumStackDepth = 513;
    private const int MaximumSubroutineDepth = 10;
    private const int MaximumHints = 96;
    private const int MaximumProgramLength = 1_048_576;

    // 與 Type2 相同的理由：深度上限不限制呼叫廣度，巢狀 subroutine 可指數展開。
    // CFF2 的程式長度上限更大（1 MiB），單層可容納的呼叫數也更多，總預算因此必要。
    private const int MaximumOperations = 1_000_000;

    internal static void Verify(
        ReadOnlyMemory<byte> charString,
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>> localSubroutines,
        int[] variationRegionCounts,
        int defaultVariationIndex)
        => Verify(
            charString,
            globalSubroutines,
            localSubroutines,
            variationRegionCounts,
            defaultVariationIndex,
            CancellationToken.None);

    internal static void Verify(
        ReadOnlyMemory<byte> charString,
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>> localSubroutines,
        int[] variationRegionCounts,
        int defaultVariationIndex,
        CancellationToken cancellationToken)
    {
        if (globalSubroutines.Count > 65_535
            || localSubroutines.Count > 65_535
            || variationRegionCounts.Length == 0 && defaultVariationIndex != 0
            || variationRegionCounts.Length != 0
                && (uint)defaultVariationIndex >= (uint)variationRegionCounts.Length)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-context");
        }

        var state = new VerificationState(
            globalSubroutines,
            localSubroutines,
            variationRegionCounts,
            defaultVariationIndex,
            cancellationToken);
        ProcessProgram(charString, state, depth: 0, isSubroutine: false);
        if (state.Stack.Count != 0)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-trailing-stack");
        }
    }

    private static void ProcessProgram(
        ReadOnlyMemory<byte> programMemory,
        VerificationState state,
        int depth,
        bool isSubroutine)
    {
        ReadOnlySpan<byte> program = programMemory.Span;
        if (program.Length > MaximumProgramLength)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-length");
        }

        int position = 0;
        while (position < program.Length)
        {
            state.ConsumeOperation();
            byte value = program[position++];
            if (value == 28 || value >= 32)
            {
                Push(state, ReadNumber(program, ref position, value));
                continue;
            }

            switch (value)
            {
                case 1:
                case 3:
                case 18:
                case 23:
                    ProcessStem(state);
                    break;
                case 4:
                    RequireExactAndClear(state, 1, "vmoveto");
                    break;
                case 5:
                    RequireMultipleAndClear(state, 2, 2, "rlineto");
                    break;
                case 6:
                case 7:
                    RequireMinimumAndClear(state, 1, "hlineto-vlineto");
                    break;
                case 8:
                    RequireMultipleAndClear(state, 6, 6, "rrcurveto");
                    break;
                case 10:
                    CallSubroutine(state, state.LocalSubroutines, depth);
                    break;
                case 12:
                    EnsureRange(program, position, 1, "escape");
                    ProcessEscape(state, program[position++]);
                    break;
                case 15:
                    ProcessVsIndex(state);
                    break;
                case 16:
                    ProcessBlend(state);
                    break;
                case 19:
                case 20:
                    ProcessHintMask(state, program, ref position);
                    break;
                case 21:
                    RequireExactAndClear(state, 2, "rmoveto");
                    break;
                case 22:
                    RequireExactAndClear(state, 1, "hmoveto");
                    break;
                case 24:
                    RequirePatternAndClear(state, 8, 2, 6, "rcurveline");
                    break;
                case 25:
                    RequirePatternAndClear(state, 8, 6, 2, "rlinecurve");
                    break;
                case 26:
                case 27:
                    RequireCurveWithOptionalCoordinateAndClear(state, "vvcurveto-hhcurveto");
                    break;
                case 29:
                    CallSubroutine(state, state.GlobalSubroutines, depth);
                    break;
                case 30:
                case 31:
                    RequireAlternatingCurveAndClear(state);
                    break;
                case 11:
                case 14:
                    throw SfntFont.DataInvalid($"CFF2-CharString-removed-operator-{value}");
                default:
                    throw SfntFont.DataInvalid($"CFF2-CharString-operator-{value}");
            }
        }

        if (!isSubroutine && state.Stack.Count != 0)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-termination");
        }
    }

    private static void CallSubroutine(
        VerificationState state,
        IReadOnlyList<ReadOnlyMemory<byte>> subroutines,
        int depth)
    {
        double? operand = Pop(state, "callsubr");
        if (operand is not double known
            || known != Math.Truncate(known)
            || known < int.MinValue
            || known > int.MaxValue)
        {
            throw SfntFont.DataInvalid("CFF2-Subrs-index");
        }

        int biasedIndex = checked((int)known + GetSubroutineBias(subroutines.Count));
        if ((uint)biasedIndex >= (uint)subroutines.Count || depth >= MaximumSubroutineDepth)
        {
            throw SfntFont.DataInvalid("CFF2-Subrs-index-depth");
        }

        ProcessProgram(subroutines[biasedIndex], state, depth + 1, isSubroutine: true);
    }

    private static void ProcessStem(VerificationState state)
    {
        if (state.Stack.Count < 2 || (state.Stack.Count & 1) != 0)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-stem");
        }

        state.HintCount = checked(state.HintCount + (state.Stack.Count / 2));
        if (state.HintCount > MaximumHints)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-hints");
        }

        state.Stack.Clear();
    }

    private static void ProcessHintMask(VerificationState state, ReadOnlySpan<byte> program, ref int position)
    {
        if (state.Stack.Count != 0)
        {
            if ((state.Stack.Count & 1) != 0)
            {
                throw SfntFont.DataInvalid("CFF2-CharString-hintmask-stem");
            }

            state.HintCount = checked(state.HintCount + (state.Stack.Count / 2));
            state.Stack.Clear();
        }

        if (state.HintCount == 0)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-hintmask-stem");
        }

        if (state.HintCount > MaximumHints)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-hintmask-count");
        }

        int maskBytes = (state.HintCount + 7) / 8;
        EnsureRange(program, position, maskBytes, "hintmask");
        position += maskBytes;
    }

    private static void ProcessVsIndex(VerificationState state)
    {
        if (state.VsIndexSeen || state.BlendSeen)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-vsindex-order");
        }

        int index = RequireInteger(
            Pop(state, "vsindex"),
            0,
            state.VariationRegionCounts.Length - 1,
            "vsindex");
        state.ActiveVariationIndex = index;
        state.VsIndexSeen = true;
    }

    private static void ProcessBlend(VerificationState state)
    {
        if (state.VariationRegionCounts.Length == 0)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-blend-without-vstore");
        }

        int valueCount = RequireInteger(Pop(state, "blend-count"), 1, MaximumStackDepth, "blend-count");
        int regionCount = state.VariationRegionCounts[state.ActiveVariationIndex];
        int operandCount = checked(valueCount + (valueCount * regionCount));
        if (operandCount > state.Stack.Count)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-blend-stack");
        }

        int start = state.Stack.Count - operandCount;
        state.Stack.RemoveRange(start, operandCount);
        for (int index = 0; index < valueCount; index++)
        {
            Push(state, null);
        }

        state.BlendSeen = true;
    }

    private static void ProcessEscape(VerificationState state, byte operation)
    {
        switch (operation)
        {
            case 34:
                RequireExactAndClear(state, 7, "hflex");
                break;
            case 35:
                RequireExactAndClear(state, 13, "flex");
                break;
            case 36:
                RequireExactAndClear(state, 9, "hflex1");
                break;
            case 37:
                RequireExactAndClear(state, 11, "flex1");
                break;
            default:
                throw SfntFont.DataInvalid($"CFF2-CharString-escape-{operation}");
        }
    }

    private static void RequireCurveWithOptionalCoordinateAndClear(VerificationState state, string detail)
    {
        int count = state.Stack.Count;
        if (count < 4 || count % 4 is not (0 or 1))
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireAlternatingCurveAndClear(VerificationState state)
    {
        int remainder = state.Stack.Count % 8;
        if (state.Stack.Count < 4 || remainder is not (0 or 1 or 4 or 5))
        {
            throw SfntFont.DataInvalid("CFF2-CharString-hv-vhcurveto");
        }

        state.Stack.Clear();
    }

    private static void RequirePatternAndClear(
        VerificationState state,
        int minimum,
        int suffix,
        int repeating,
        string detail)
    {
        if (state.Stack.Count < minimum || (state.Stack.Count - suffix) % repeating != 0)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireMultipleAndClear(
        VerificationState state,
        int minimum,
        int multiple,
        string detail)
    {
        if (state.Stack.Count < minimum || state.Stack.Count % multiple != 0)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireMinimumAndClear(VerificationState state, int minimum, string detail)
    {
        if (state.Stack.Count < minimum)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireExactAndClear(VerificationState state, int count, string detail)
    {
        if (state.Stack.Count != count)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static double? ReadNumber(ReadOnlySpan<byte> program, ref int position, byte first)
    {
        if (first is >= 32 and <= 246)
        {
            return first - 139;
        }

        if (first is >= 247 and <= 250)
        {
            EnsureRange(program, position, 1, "number");
            return ((first - 247) * 256) + program[position++] + 108;
        }

        if (first is >= 251 and <= 254)
        {
            EnsureRange(program, position, 1, "number");
            return -((first - 251) * 256) - program[position++] - 108;
        }

        if (first == 28)
        {
            EnsureRange(program, position, 2, "number");
            double result = BinaryPrimitives.ReadInt16BigEndian(program.Slice(position, 2));
            position += 2;
            return result;
        }

        if (first == 255)
        {
            EnsureRange(program, position, 4, "number");
            double result = BinaryPrimitives.ReadInt32BigEndian(program.Slice(position, 4)) / 65_536d;
            position += 4;
            return result;
        }

        throw SfntFont.DataInvalid("CFF2-CharString-number");
    }

    private static void Push(VerificationState state, double? value)
    {
        if (state.Stack.Count >= MaximumStackDepth)
        {
            throw SfntFont.DataInvalid("CFF2-CharString-stack");
        }

        state.Stack.Add(value);
    }

    private static double? Pop(VerificationState state, string detail)
    {
        if (state.Stack.Count == 0)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}-stack");
        }

        int index = state.Stack.Count - 1;
        double? value = state.Stack[index];
        state.Stack.RemoveAt(index);
        return value;
    }

    private static int RequireInteger(double? value, int minimum, int maximum, string detail)
    {
        if (value is not double known
            || known != Math.Truncate(known)
            || known < minimum
            || known > maximum)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}");
        }

        return (int)known;
    }

    private static int GetSubroutineBias(int count)
        => count < 1_240 ? 107 : count < 33_900 ? 1_131 : 32_768;

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length, string detail)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw SfntFont.DataInvalid($"CFF2-CharString-{detail}-range");
        }
    }

    private sealed class VerificationState(
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>> localSubroutines,
        int[] variationRegionCounts,
        int defaultVariationIndex,
        CancellationToken cancellationToken)
    {
        private int _remainingOperations = MaximumOperations;

        /// <summary>
        /// 扣減總操作預算；耗盡時拒絕，並定期檢查取消要求。
        /// </summary>
        internal void ConsumeOperation()
        {
            if (--_remainingOperations <= 0)
            {
                throw SfntFont.DataInvalid("CFF2-CharString-operation-budget");
            }

            if ((_remainingOperations & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        internal IReadOnlyList<ReadOnlyMemory<byte>> GlobalSubroutines { get; } = globalSubroutines;

        internal IReadOnlyList<ReadOnlyMemory<byte>> LocalSubroutines { get; } = localSubroutines;

        internal int[] VariationRegionCounts { get; } = variationRegionCounts;

        internal List<double?> Stack { get; } = new(MaximumStackDepth);

        internal int ActiveVariationIndex { get; set; } = defaultVariationIndex;

        internal int HintCount { get; set; }

        internal bool BlendSeen { get; set; }

        internal bool VsIndexSeen { get; set; }
    }
}
