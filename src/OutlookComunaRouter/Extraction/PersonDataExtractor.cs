using System.Text.RegularExpressions;

namespace OutlookComunaRouter.Extraction;

public sealed record ExtractedPersonData(string? FullName, string? Rut);

/// <summary>
/// Calibrated against real comuna emails (38 production messages, 2026-07-03):
/// the RUT arrives prefixed (RUT/RUN/R.U.T.), bare with dots, or bare without dots;
/// the contributor's name has no reliable marker of its own, but reliably sits
/// adjacent to the RUT. Matching "first capitalized words anywhere" is NOT viable —
/// it grabs the Exchange "CORREO EXTERNO" banner or forwarded-header sender names.
/// </summary>
public static partial class PersonDataExtractor
{
    // Exchange prepends this warning to every external email; strip it before extracting.
    [GeneratedRegex(@"CORREO EXTERNO\s*:?.*?(seguro\.|adjuntos?\.)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ExternalBannerPattern();

    // Priority 1: prefixed — RUT: / R.U.T. / RUN / R.U.N., dots and spacing optional.
    [GeneratedRegex(@"R\.?\s?U\.?\s?[TN]\.?\s*:?\s*(\d{1,2}(?:\.?\d{3}){2}\s?-\s?[\dkK])", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixedRutPattern();

    // Priority 2: bare dotted (12.345.678-9) — unambiguous even without a prefix.
    [GeneratedRegex(@"\b(\d{1,2}\.\d{3}\.\d{3}\s?-\s?[\dkK])\b")]
    private static partial Regex BareDottedRutPattern();

    // Priority 3: bare undotted (12345678-9).
    [GeneratedRegex(@"\b(\d{7,8}\s?-\s?[\dkK])\b")]
    private static partial Regex BareUndottedRutPattern();

    // 2-5 capitalized words (all-caps or title case), used only in the window adjacent to the RUT.
    [GeneratedRegex(@"([A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ]+){1,4})")]
    private static partial Regex NameSequencePattern();

    // Honorifics and connectors stripped from the edges of a name candidate.
    private static readonly string[] EdgeStopWords =
        ["DON", "DOÑA", "SR", "SRA", "SEÑOR", "SEÑORA", "DE", "DEL", "RUT", "RUN", "CI", "CÉDULA"];

    private const int NameWindowChars = 90;

    public static ExtractedPersonData Extract(string bodyText)
    {
        var text = ExternalBannerPattern().Replace(bodyText, " ");

        var rutMatch = FindRut(text);
        if (rutMatch is null)
        {
            // A name without an anchoring RUT is not trustworthy (banners, forwarded
            // sender names, salutations all match a generic capitalized-words pattern).
            return new ExtractedPersonData(null, null);
        }

        var (rut, matchIndex, matchLength) = rutMatch.Value;
        var fullName = FindNameNear(text, matchIndex, matchLength);
        return new ExtractedPersonData(fullName, rut);
    }

    private static (string Rut, int Index, int Length)? FindRut(string text)
    {
        foreach (var pattern in (ReadOnlySpan<Regex>)[PrefixedRutPattern(), BareDottedRutPattern(), BareUndottedRutPattern()])
        {
            foreach (Match match in pattern.Matches(text))
            {
                var normalized = RutValidator.NormalizeAndValidate(match.Groups[1].Value);
                if (normalized is not null)
                {
                    return (normalized, match.Index, match.Length);
                }
            }
        }

        return null;
    }

    private static string? FindNameNear(string text, int rutIndex, int rutLength)
    {
        var windowStart = Math.Max(0, rutIndex - NameWindowChars);
        var before = text[windowStart..rutIndex];

        // The name usually ends right where the RUT begins → take the LAST sequence before it.
        var beforeMatches = NameSequencePattern().Matches(before);
        if (beforeMatches.Count > 0)
        {
            var candidate = CleanName(beforeMatches[^1].Groups[1].Value);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        var afterStart = rutIndex + rutLength;
        var after = text[afterStart..Math.Min(text.Length, afterStart + NameWindowChars)];
        var afterMatch = NameSequencePattern().Match(after);
        return afterMatch.Success ? CleanName(afterMatch.Groups[1].Value) : null;
    }

    private static string? CleanName(string raw)
    {
        var words = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        while (words.Count > 0 && EdgeStopWords.Contains(Normalize(words[0])))
        {
            words.RemoveAt(0);
        }
        while (words.Count > 0 && EdgeStopWords.Contains(Normalize(words[^1])))
        {
            words.RemoveAt(words.Count - 1);
        }

        // A real full name has at least two words (first + last name).
        return words.Count >= 2 ? string.Join(' ', words) : null;
    }

    private static string Normalize(string word) => word.TrimEnd('.', ',', ':').ToUpperInvariant();
}
