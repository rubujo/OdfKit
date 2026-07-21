using System.Globalization;
using System.Text;

namespace OdfKit.WebFonts.OpenType;

internal static class WebFontSequenceCoverage
{
    internal static IReadOnlyList<WebFontTextSequence> Filter(
        IReadOnlyList<WebFontTextSequence> sequences,
        Func<int, bool> containsScalar,
        Func<UnicodeVariationSequence, bool> containsVariation,
        CancellationToken cancellationToken)
    {
        var supported = new List<WebFontTextSequence>();
        var run = new StringBuilder();
        foreach (WebFontTextSequence sequence in sequences)
        {
            foreach (string element in EnumerateClusters(sequence.Text))
            {
                cancellationToken.ThrowIfCancellationRequested();
                WebFontTextSequence cluster = WebFontTextSequence.Create(element);
                if (IsSupportedCluster(cluster, containsScalar, containsVariation))
                {
                    run.Append(element);
                }
                else
                {
                    Flush(run, supported);
                }
            }

            Flush(run, supported);
        }

        return supported;
    }

    internal static bool RequiresGlyph(int scalar)
        => scalar != 0x0020
            && scalar != 0x00A0
            && scalar != 0x00AD
            && scalar != 0x034F
            && scalar != 0x061C
            && scalar != 0x1680
            && scalar != 0x2028
            && scalar != 0x2029
            && scalar != 0x202F
            && scalar != 0x205F
            && scalar != 0x3000
            && scalar != 0xFEFF
            && scalar is not (>= 0x180B and <= 0x180F)
            && scalar is not (>= 0x2000 and <= 0x200A)
            && scalar is not (>= 0x200B and <= 0x200F)
            && scalar is not (>= 0x202A and <= 0x202E)
            && scalar is not (>= 0x2060 and <= 0x206F)
            && scalar is not (>= 0xFE00 and <= 0xFE0F)
            && scalar is not (>= 0xFFF9 and <= 0xFFFB)
            && scalar is not (>= 0x1BCA0 and <= 0x1BCAF)
            && scalar is not (>= 0x1D173 and <= 0x1D17A)
            && scalar is not (>= 0xE0000 and <= 0xE0FFF)
            && scalar is not (>= 0x0000 and <= 0x001F)
            && scalar is not (>= 0x007F and <= 0x009F);

    private static bool IsSupportedCluster(
        WebFontTextSequence cluster,
        Func<int, bool> containsScalar,
        Func<UnicodeVariationSequence, bool> containsVariation)
    {
        bool hasGlyph = false;
        for (int index = 0; index < cluster.UnicodeScalars.Count; index++)
        {
            int scalar = cluster.UnicodeScalars[index];
            if (IsVariationSelector(scalar))
            {
                if (index == 0
                    || !containsVariation(new UnicodeVariationSequence(
                        cluster.UnicodeScalars[index - 1],
                        scalar)))
                {
                    return false;
                }

                continue;
            }

            bool requiresGlyph = RequiresGlyph(scalar);
            if (requiresGlyph && !containsScalar(scalar))
            {
                return false;
            }

            hasGlyph |= requiresGlyph;
        }

        return hasGlyph;
    }

    private static bool IsVariationSelector(int scalar)
        => scalar is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF;

    private static IEnumerable<string> EnumerateClusters(string text)
    {
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        string? current = null;
        while (elements.MoveNext())
        {
            string next = elements.GetTextElement();
            if (current is null)
            {
                current = next;
                continue;
            }

            WebFontTextSequence currentSequence = WebFontTextSequence.Create(current);
            WebFontTextSequence nextSequence = WebFontTextSequence.Create(next);
            if (ShouldCoalesce(currentSequence, nextSequence, next))
            {
                current = string.Concat(current, next);
                continue;
            }

            yield return current;
            current = next;
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    private static bool ShouldCoalesce(
        WebFontTextSequence current,
        WebFontTextSequence next,
        string nextText)
    {
        int first = next.UnicodeScalars[0];
        int last = current.UnicodeScalars[current.UnicodeScalars.Count - 1];
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(nextText, 0);
        if (IsVariationSelector(first)
            || first == 0x200D
            || last == 0x200D
            || first is >= 0x1F3FB and <= 0x1F3FF
            || first is >= 0xE0020 and <= 0xE007F
            || category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
        {
            return true;
        }

        if (first is not (>= 0x1F1E6 and <= 0x1F1FF))
        {
            return false;
        }

        int trailingRegionalCount = 0;
        for (int index = current.UnicodeScalars.Count - 1;
             index >= 0 && current.UnicodeScalars[index] is >= 0x1F1E6 and <= 0x1F1FF;
             index--)
        {
            trailingRegionalCount++;
        }
        return trailingRegionalCount % 2 == 1;
    }

    private static void Flush(StringBuilder run, ICollection<WebFontTextSequence> supported)
    {
        if (run.Length == 0)
        {
            return;
        }

        supported.Add(WebFontTextSequence.Create(run.ToString()));
        run.Clear();
    }
}
