using System.Text;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

/// <summary>P8b: one medicine in the bundled list of medicines registered in Egypt.</summary>
public sealed record DrugEntry(
    string Name, string ArabicName, string ScientificName, string Manufacturer, string DrugClass, string Route, decimal Price);

/// <summary>
/// P8b: searches the drug list in memory. Every word typed must appear in the trade name, the Arabic name or the
/// active ingredient. Results that start with what was typed come first, then trade names with a word starting
/// with it, then other name matches, then matches on the active ingredient only. Arabic is compared without
/// diacritics, and with the usual letter variants treated alike (أ إ آ ا, ى ي, ة ه).
/// </summary>
public sealed class DrugIndex
{
    private sealed record Indexed(DrugEntry Entry, string Name, string Arabic, string All);

    private readonly IReadOnlyList<Indexed> _items;

    public DrugIndex(IEnumerable<DrugEntry> entries)
    {
        _items = entries
            .Select(e =>
            {
                var name = Normalize(e.Name);
                var arabic = Normalize(e.ArabicName);
                return new Indexed(e, name, arabic, $"{name} {arabic} {Normalize(e.ScientificName)}");
            })
            .ToList();
    }

    public int Count => _items.Count;

    public IReadOnlyList<DrugEntry> Find(string query, int max)
    {
        var q = Normalize(query);
        if (q.Length == 0 || max <= 0) return Array.Empty<DrugEntry>();
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = words[0];

        return _items
            .Where(i => words.All(w => i.All.Contains(w, StringComparison.Ordinal)))
            .Select(i => (Item: i, Score: Score(i, q, first)))
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Name.Length)
            .ThenBy(x => x.Item.Name, StringComparer.Ordinal)
            .Take(max)
            .Select(x => x.Item.Entry)
            .ToList();
    }

    private static int Score(Indexed i, string q, string first)
    {
        if (i.Name.StartsWith(q, StringComparison.Ordinal)) return 0;
        if (i.Arabic.StartsWith(q, StringComparison.Ordinal)) return 1;
        if (HasWordStarting(i.Name, first) || HasWordStarting(i.Arabic, first)) return 2;
        if (i.Name.Contains(first, StringComparison.Ordinal) || i.Arabic.Contains(first, StringComparison.Ordinal)) return 3;
        return 4;
    }

    private static bool HasWordStarting(string text, string word)
    {
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
            if (at == 0 || !char.IsLetterOrDigit(text[at - 1])) return true;
        return false;
    }

    /// <summary>Lower case, single spaces, Arabic without diacritics and with letter variants unified.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var raw in text.Trim().ToLowerInvariant())
        {
            var c = raw switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                _ => raw
            };
            if (c is >= 'ً' and <= 'ْ' || c == 'ـ') continue;   // diacritics, tatweel
            if (char.IsWhiteSpace(c))
            {
                if (!space) sb.Append(' ');
                space = true;
                continue;
            }
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
