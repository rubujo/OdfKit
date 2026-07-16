namespace OdfKit.WebFonts.OpenType;

internal static class UnicodeRangeFormatter
{
    internal static IReadOnlyList<string> Create(IEnumerable<int> scalars)
    {
        int[] values = scalars.Distinct().OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return Array.Empty<string>();
        }

        var ranges = new List<string>();
        int start = values[0];
        int end = start;
        for (int index = 1; index < values.Length; index++)
        {
            int value = values[index];
            if (value == end + 1)
            {
                end = value;
                continue;
            }

            ranges.Add(Format(start, end));
            start = value;
            end = value;
        }

        ranges.Add(Format(start, end));
        return ranges;
    }

    private static string Format(int start, int end)
        => start == end ? $"U+{start:X}" : $"U+{start:X}-{end:X}";
}
