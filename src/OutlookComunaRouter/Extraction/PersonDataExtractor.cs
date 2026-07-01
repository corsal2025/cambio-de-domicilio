using System.Text.RegularExpressions;

namespace OutlookComunaRouter.Extraction;

public sealed record ExtractedPersonData(string? FullName, string? Rut);

public static partial class PersonDataExtractor
{
    // Matches "RUT: 18.785.387-7", "RUT 18785387-7", "Rut: 18.785.387-K", etc.
    [GeneratedRegex(@"RUT\s*:?\s*(\d{1,2}(?:\.?\d{3}){2}-?[\dkK])", RegexOptions.IgnoreCase)]
    private static partial Regex RutPattern();

    // 2-4 words, each starting with an uppercase letter and continuing with letters of
    // either case, so it accepts both "GUSTAVO ANDRÉS PEÑA CASTRO" (all caps) and
    // "Gustavo Andrés Peña Castro" (title case). "RUT" is excluded so it never gets
    // swallowed into the name when it immediately follows (e.g. "... CASTRO RUT: 1-9").
    [GeneratedRegex(@"\b((?!RUT\b)[A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ]+(?:\s+(?!RUT\b)[A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑa-záéíóúñ]+){1,3})\b")]
    private static partial Regex NamePattern();

    public static ExtractedPersonData Extract(string bodyText)
    {
        string? rut = null;
        var rutMatch = RutPattern().Match(bodyText);
        if (rutMatch.Success)
        {
            rut = RutValidator.NormalizeAndValidate(rutMatch.Groups[1].Value);
        }

        string? fullName = null;
        var nameMatch = NamePattern().Match(bodyText);
        if (nameMatch.Success)
        {
            fullName = nameMatch.Groups[1].Value.Trim();
        }

        return new ExtractedPersonData(fullName, rut);
    }
}
