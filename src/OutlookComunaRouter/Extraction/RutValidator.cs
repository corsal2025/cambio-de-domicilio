using System.Text;

namespace OutlookComunaRouter.Extraction;

public static class RutValidator
{
    /// <summary>
    /// Normalizes a Chilean RUT (with or without dots, upper/lowercase K) to canonical
    /// dotted form (e.g. "18.785.387-7") and validates its check digit.
    /// Returns null if the input is not shaped like a RUT or fails the check digit.
    /// </summary>
    public static string? NormalizeAndValidate(string rawRut)
    {
        var digitsAndK = new StringBuilder();
        foreach (var c in rawRut)
        {
            if (char.IsDigit(c) || c is 'k' or 'K')
            {
                digitsAndK.Append(char.ToUpperInvariant(c));
            }
        }

        if (digitsAndK.Length < 2)
        {
            return null;
        }

        var body = digitsAndK.ToString(0, digitsAndK.Length - 1);
        var checkDigit = digitsAndK[^1];

        if (body.Length is < 7 or > 8 || !body.All(char.IsDigit))
        {
            return null;
        }

        if (ComputeCheckDigit(body) != checkDigit)
        {
            return null;
        }

        // RUTs under 10 million have a 7-digit body — pad with a leading 0 so every RUT
        // groups into the same 2-3-3-digit shape (e.g. "9.876.543" -> "09.876.543").
        if (body.Length == 7)
        {
            body = "0" + body;
        }

        return Format(body, checkDigit);
    }

    private static char ComputeCheckDigit(string body)
    {
        var sum = 0;
        var multiplier = 2;
        for (var i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * multiplier;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        var remainder = 11 - (sum % 11);
        return remainder switch
        {
            11 => '0',
            10 => 'K',
            _ => (char)('0' + remainder)
        };
    }

    private static string Format(string body, char checkDigit)
    {
        var reversed = new string(body.Reverse().ToArray());
        var grouped = new StringBuilder();
        for (var i = 0; i < reversed.Length; i++)
        {
            if (i > 0 && i % 3 == 0)
            {
                grouped.Append('.');
            }
            grouped.Append(reversed[i]);
        }
        var formattedBody = new string(grouped.ToString().Reverse().ToArray());
        return $"{formattedBody}-{checkDigit}";
    }
}
