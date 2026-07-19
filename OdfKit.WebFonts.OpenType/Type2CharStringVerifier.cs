using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class Type2CharStringVerifier
{
    private const int MaximumStackDepth = 48;
    private const int MaximumSubroutineDepth = 10;
    private const int MaximumHints = 96;
    private const int MaximumCharStringLength = 65_535;

    internal static Type2SeacComponents? Verify(
        ReadOnlyMemory<byte> charString,
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>> localSubroutines)
    {
        if (globalSubroutines.Count > 65_535 || localSubroutines.Count > 65_535)
        {
            throw SfntFont.DataInvalid("CFF-Subrs-count");
        }

        var state = new VerificationState(globalSubroutines, localSubroutines);
        ProgramResult result = ProcessProgram(charString, state, depth: 0, isSubroutine: false);
        if (result != ProgramResult.EndChar)
        {
            throw SfntFont.DataInvalid("CFF-CharString-termination");
        }

        return state.SeacComponents;
    }

    private static ProgramResult ProcessProgram(
        ReadOnlyMemory<byte> programMemory,
        VerificationState state,
        int depth,
        bool isSubroutine)
    {
        ReadOnlySpan<byte> program = programMemory.Span;
        if (program.Length > MaximumCharStringLength)
        {
            throw SfntFont.DataInvalid("CFF-CharString-length");
        }

        int position = 0;
        while (position < program.Length)
        {
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
                    ProcessMove(state, 1);
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
                    {
                        ProgramResult result = CallSubroutine(state, state.LocalSubroutines, depth);
                        if (result == ProgramResult.EndChar)
                        {
                            return result;
                        }

                        break;
                    }
                case 11:
                    if (!isSubroutine)
                    {
                        throw SfntFont.DataInvalid("CFF-CharString-return");
                    }

                    return ProgramResult.Return;
                case 12:
                    EnsureRange(program, position, 1, "escape");
                    ProcessEscape(state, program[position++]);
                    break;
                case 14:
                    return ProcessEndChar(state);
                case 19:
                case 20:
                    ProcessHintMask(state, program, ref position);
                    break;
                case 21:
                    ProcessMove(state, 2);
                    break;
                case 22:
                    ProcessMove(state, 1);
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
                    {
                        ProgramResult result = CallSubroutine(state, state.GlobalSubroutines, depth);
                        if (result == ProgramResult.EndChar)
                        {
                            return result;
                        }

                        break;
                    }
                case 30:
                case 31:
                    RequireAlternatingCurveAndClear(state);
                    break;
                default:
                    throw SfntFont.DataInvalid($"CFF-CharString-operator-{value}");
            }
        }

        return ProgramResult.Continue;
    }

    private static ProgramResult CallSubroutine(
        VerificationState state,
        IReadOnlyList<ReadOnlyMemory<byte>> subroutines,
        int depth)
    {
        double? operand = Pop(state, "callsubr");
        if (operand is not double known || known != Math.Truncate(known))
        {
            throw SfntFont.DataInvalid("CFF-Subrs-index");
        }

        int biasedIndex = checked((int)known + GetSubroutineBias(subroutines.Count));
        if ((uint)biasedIndex >= (uint)subroutines.Count || depth >= MaximumSubroutineDepth)
        {
            throw SfntFont.DataInvalid("CFF-Subrs-index-depth");
        }

        ProgramResult result = ProcessProgram(subroutines[biasedIndex], state, depth + 1, isSubroutine: true);
        return result == ProgramResult.Continue ? throw SfntFont.DataInvalid("CFF-Subrs-termination") : result;
    }

    private static void ProcessStem(VerificationState state)
    {
        ConsumeOptionalWidth(state, expectedOperands: 0, pairOperands: true);
        if (state.Stack.Count < 2 || (state.Stack.Count & 1) != 0)
        {
            throw SfntFont.DataInvalid("CFF-CharString-stem");
        }

        state.HintCount = checked(state.HintCount + (state.Stack.Count / 2));
        if (state.HintCount > MaximumHints)
        {
            throw SfntFont.DataInvalid("CFF-CharString-hints");
        }

        state.Stack.Clear();
    }

    private static void ProcessHintMask(VerificationState state, ReadOnlySpan<byte> program, ref int position)
    {
        if (state.Stack.Count != 0)
        {
            ConsumeOptionalWidth(state, expectedOperands: 0, pairOperands: true);
            if ((state.Stack.Count & 1) != 0)
            {
                throw SfntFont.DataInvalid("CFF-CharString-hintmask-stem");
            }

            state.HintCount = checked(state.HintCount + (state.Stack.Count / 2));
            state.Stack.Clear();
        }

        if (state.HintCount > MaximumHints)
        {
            throw SfntFont.DataInvalid("CFF-CharString-hintmask-count");
        }

        int maskBytes = (state.HintCount + 7) / 8;
        EnsureRange(program, position, maskBytes, "hintmask");
        position += maskBytes;
    }

    private static void ProcessMove(VerificationState state, int expectedOperands)
    {
        ConsumeOptionalWidth(state, expectedOperands, pairOperands: false);
        if (state.Stack.Count != expectedOperands)
        {
            throw SfntFont.DataInvalid("CFF-CharString-moveto");
        }

        state.Stack.Clear();
    }

    private static ProgramResult ProcessEndChar(VerificationState state)
    {
        int count = state.Stack.Count;
        bool hasWidth = !state.WidthSeen && count is 1 or 5;
        bool hasSeac = count is 4 or 5;
        if (hasSeac)
        {
            if (count == 5 && state.WidthSeen)
            {
                throw SfntFont.DataInvalid("CFF-CharString-endchar-width");
            }

            int componentOffset = count - 2;
            int baseCode = RequireInteger(
                state.Stack[componentOffset],
                byte.MinValue,
                byte.MaxValue,
                "seac-bchar");
            int accentCode = RequireInteger(
                state.Stack[componentOffset + 1],
                byte.MinValue,
                byte.MaxValue,
                "seac-achar");
            state.SeacComponents = new Type2SeacComponents(
                checked((byte)baseCode),
                checked((byte)accentCode));
        }
        else if (count != 0 && !hasWidth)
        {
            throw SfntFont.DataInvalid("CFF-CharString-endchar");
        }

        state.WidthSeen = true;
        state.Stack.Clear();
        return ProgramResult.EndChar;
    }

    private static void ConsumeOptionalWidth(VerificationState state, int expectedOperands, bool pairOperands)
    {
        if (state.WidthSeen)
        {
            return;
        }

        bool hasWidth = pairOperands
            ? (state.Stack.Count & 1) != 0
            : state.Stack.Count == expectedOperands + 1;
        if (hasWidth)
        {
            state.Stack.RemoveAt(0);
        }

        state.WidthSeen = true;
    }

    private static void ProcessEscape(VerificationState state, byte operation)
    {
        switch (operation)
        {
            case 0:
                RequireCount(state, 0, "dotsection");
                break;
            case 3:
                Binary(state, (left, right) => Boolean(left) && Boolean(right) ? 1 : 0, "and");
                break;
            case 4:
                Binary(state, (left, right) => Boolean(left) || Boolean(right) ? 1 : 0, "or");
                break;
            case 5:
                Unary(state, value => Boolean(value) ? 0 : 1, "not");
                break;
            case 9:
                Unary(state, value => Math.Abs(value), "abs");
                break;
            case 10:
                Binary(state, (left, right) => left + right, "add");
                break;
            case 11:
                Binary(state, (left, right) => left - right, "sub");
                break;
            case 12:
                Divide(state);
                break;
            case 14:
                Unary(state, value => -value, "neg");
                break;
            case 15:
                Binary(state, (left, right) => left == right ? 1 : 0, "eq");
                break;
            case 18:
                _ = Pop(state, "drop");
                break;
            case 20:
                Put(state);
                break;
            case 21:
                Get(state);
                break;
            case 22:
                IfElse(state);
                break;
            case 23:
                Push(state, null);
                break;
            case 24:
                Binary(state, (left, right) => left * right, "mul");
                break;
            case 26:
                SquareRoot(state);
                break;
            case 27:
                Push(state, Peek(state, 0, "dup"));
                break;
            case 28:
                Exchange(state);
                break;
            case 29:
                Index(state);
                break;
            case 30:
                Roll(state);
                break;
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
                throw SfntFont.DataInvalid($"CFF-CharString-escape-{operation}");
        }
    }

    private static void Unary(VerificationState state, Func<double, double?> operation, string detail)
    {
        double? value = Pop(state, detail);
        Push(state, value is double known ? operation(known) : null);
    }

    private static void Binary(
        VerificationState state,
        Func<double, double, double?> operation,
        string detail)
    {
        double? right = Pop(state, detail);
        double? left = Pop(state, detail);
        Push(state, left is double knownLeft && right is double knownRight
            ? operation(knownLeft, knownRight)
            : null);
    }

    private static void Divide(VerificationState state)
    {
        double? divisor = Pop(state, "div");
        double? dividend = Pop(state, "div");
        if (divisor is 0d)
        {
            throw SfntFont.DataInvalid("CFF-CharString-div-zero");
        }

        Push(state, dividend is double knownDividend && divisor is double knownDivisor
            ? knownDividend / knownDivisor
            : null);
    }

    private static void SquareRoot(VerificationState state)
    {
        double? value = Pop(state, "sqrt");
        if (value is < 0d)
        {
            throw SfntFont.DataInvalid("CFF-CharString-sqrt-negative");
        }

        Push(state, value is double known ? Math.Sqrt(known) : null);
    }

    private static void Put(VerificationState state)
    {
        double? indexValue = Pop(state, "put-index");
        double? value = Pop(state, "put-value");
        int index = RequireInteger(indexValue, 0, state.Transient.Length - 1, "put-index");
        state.Transient[index] = value;
    }

    private static void Get(VerificationState state)
    {
        int index = RequireInteger(Pop(state, "get-index"), 0, state.Transient.Length - 1, "get-index");
        Push(state, state.Transient[index]);
    }

    private static void IfElse(VerificationState state)
    {
        double? secondComparison = Pop(state, "ifelse");
        double? firstComparison = Pop(state, "ifelse");
        double? secondValue = Pop(state, "ifelse");
        double? firstValue = Pop(state, "ifelse");
        Push(state, firstComparison is double first && secondComparison is double second
            ? first <= second ? firstValue : secondValue
            : null);
    }

    private static void Exchange(VerificationState state)
    {
        if (state.Stack.Count < 2)
        {
            throw SfntFont.DataInvalid("CFF-CharString-exch");
        }

        int top = state.Stack.Count - 1;
        (state.Stack[top - 1], state.Stack[top]) = (state.Stack[top], state.Stack[top - 1]);
    }

    private static void Index(VerificationState state)
    {
        double? operand = Pop(state, "index");
        if (state.Stack.Count == 0)
        {
            throw SfntFont.DataInvalid("CFF-CharString-index-stack");
        }

        if (operand is not double known)
        {
            Push(state, null);
            return;
        }

        if (known != Math.Truncate(known) || known < int.MinValue || known > int.MaxValue)
        {
            throw SfntFont.DataInvalid("CFF-CharString-index");
        }

        int index = (int)known;
        index = index < 0 ? 0 : index >= state.Stack.Count ? state.Stack.Count - 1 : index;
        Push(state, Peek(state, index, "index"));
    }

    private static void Roll(VerificationState state)
    {
        int shift = RequireInteger(Pop(state, "roll-shift"), int.MinValue, int.MaxValue, "roll-shift");
        int count = RequireInteger(Pop(state, "roll-count"), 0, state.Stack.Count, "roll-count");
        if (count == 0)
        {
            return;
        }

        int normalized = ((shift % count) + count) % count;
        if (normalized == 0)
        {
            return;
        }

        int start = state.Stack.Count - count;
        double?[] values = state.Stack.GetRange(start, count).ToArray();
        for (int index = 0; index < count; index++)
        {
            state.Stack[start + ((index + normalized) % count)] = values[index];
        }
    }

    private static void RequireCurveWithOptionalCoordinateAndClear(VerificationState state, string detail)
    {
        int count = state.Stack.Count;
        if (count < 4 || count % 4 is not (0 or 1))
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireAlternatingCurveAndClear(VerificationState state)
    {
        int remainder = state.Stack.Count % 8;
        if (state.Stack.Count < 4 || remainder is not (0 or 1 or 4 or 5))
        {
            throw SfntFont.DataInvalid("CFF-CharString-hv-vhcurveto");
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
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
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
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireMinimumAndClear(VerificationState state, int minimum, string detail)
    {
        if (state.Stack.Count < minimum)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
        }

        state.Stack.Clear();
    }

    private static void RequireExactAndClear(VerificationState state, int count, string detail)
    {
        RequireCount(state, count, detail);
        state.Stack.Clear();
    }

    private static void RequireCount(VerificationState state, int count, string detail)
    {
        if (state.Stack.Count != count)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
        }
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

        throw SfntFont.DataInvalid("CFF-CharString-number");
    }

    private static void Push(VerificationState state, double? value)
    {
        if (state.Stack.Count >= MaximumStackDepth)
        {
            throw SfntFont.DataInvalid("CFF-CharString-stack");
        }

        state.Stack.Add(value);
    }

    private static double? Pop(VerificationState state, string detail)
    {
        if (state.Stack.Count == 0)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}-stack");
        }

        int index = state.Stack.Count - 1;
        double? value = state.Stack[index];
        state.Stack.RemoveAt(index);
        return value;
    }

    private static double? Peek(VerificationState state, int depth, string detail)
    {
        int index = state.Stack.Count - 1 - depth;
        if (index < 0)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}-stack");
        }

        return state.Stack[index];
    }

    private static int RequireInteger(double? value, int minimum, int maximum, string detail)
    {
        if (value is not double known
            || known != Math.Truncate(known)
            || known < minimum
            || known > maximum)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}");
        }

        return checked((int)known);
    }

    private static bool Boolean(double value) => value != 0;

    private static int GetSubroutineBias(int count)
        => count < 1_240 ? 107 : count < 33_900 ? 1_131 : 32_768;

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length, string detail)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw SfntFont.DataInvalid($"CFF-CharString-{detail}-range");
        }
    }

    private sealed class VerificationState(
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>> localSubroutines)
    {
        internal IReadOnlyList<ReadOnlyMemory<byte>> GlobalSubroutines { get; } = globalSubroutines;

        internal IReadOnlyList<ReadOnlyMemory<byte>> LocalSubroutines { get; } = localSubroutines;

        internal List<double?> Stack { get; } = new(MaximumStackDepth);

        internal double?[] Transient { get; } = new double?[32];

        internal int HintCount { get; set; }

        internal bool WidthSeen { get; set; }

        internal Type2SeacComponents? SeacComponents { get; set; }
    }

    private enum ProgramResult
    {
        Continue,
        Return,
        EndChar
    }
}

internal readonly struct Type2SeacComponents
{
    internal Type2SeacComponents(byte baseCode, byte accentCode)
    {
        BaseCode = baseCode;
        AccentCode = accentCode;
    }

    internal byte BaseCode { get; }

    internal byte AccentCode { get; }
}
