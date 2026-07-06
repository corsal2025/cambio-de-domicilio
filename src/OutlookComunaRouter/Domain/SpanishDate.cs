namespace OutlookComunaRouter.Domain;

/// <summary>
/// Free-text Spanish date entry ("15 marzo 2024", "15 de marzo de 2024") — the operator
/// types the última-carpeta date by hand; a calendar picker was explicitly rejected as slower.
/// </summary>
public static class SpanishDate
{
    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
        ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
        ["septiembre"] = 9, ["setiembre"] = 9, ["octubre"] = 10,
        ["noviembre"] = 11, ["diciembre"] = 12
    };

    private static readonly string[] MonthNames =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio",
         "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    /// <summary>Two-digit years at or above this value resolve to 19xx; below it, to 20xx.</summary>
    private const int TwoDigitYearPivot = 50;

    public static bool TryParse(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var words = input
            .Split([' ', '\t', ',', '/', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !w.Equals("de", StringComparison.OrdinalIgnoreCase)
                     && !w.Equals("del", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (words.Length != 3 || !int.TryParse(words[0], out var day))
        {
            return false;
        }

        if (!Months.TryGetValue(words[1], out var month)
            && (!int.TryParse(words[1], out month) || month < 1 || month > 12))
        {
            return false;
        }

        if (!int.TryParse(words[2], out var year))
        {
            return false;
        }

        if (words[2].Length <= 2)
        {
            year += year >= TwoDigitYearPivot ? 1900 : 2000;
        }

        if (year < 1900 || year > 2100 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>Formats as the operator expects to read it: "15 marzo 2024".</summary>
    public static string Format(DateOnly date) => $"{date.Day} {MonthNames[date.Month - 1]} {date.Year}";
}
