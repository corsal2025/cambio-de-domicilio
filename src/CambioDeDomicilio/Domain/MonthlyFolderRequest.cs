using System.Globalization;

namespace CambioDeDomicilio.Domain;

/// <summary>
/// Rules of the monthly folder request ("Pedido mensual"), the F8 counterpart of the "Impresión mensual"
/// of F8 Urgentes: every month the operator prints the list of F8 cases whose folder was uploaded in the
/// previous month and requests the corresponding folders.
/// </summary>
public static class MonthlyFolderRequest
{
    /// <summary>The reminder is shown from day 1 through this day of the month.</summary>
    public const int ReminderLastDay = 10;

    private const string MonthFormat = "yyyy-MM";

    public static string MonthKey(DateTimeOffset value) =>
        value.LocalDateTime.ToString(MonthFormat, CultureInfo.InvariantCulture);

    public static string PreviousMonthKey(DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        return new DateTime(local.Year, local.Month, 1).AddMonths(-1)
            .ToString(MonthFormat, CultureInfo.InvariantCulture);
    }

    public static bool IsValidMonthKey(string? value) =>
        DateTime.TryParseExact(value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
        && value!.Length == MonthFormat.Length;

    /// <summary>True when the case must be listed for <paramref name="monthKey"/> (yyyy-MM): in F8 or
    /// Última Carpeta, not closed without folder, uploaded or confirmed, with a last-folder date, and
    /// uploaded (or, failing that, confirmed) in that month.</summary>
    public static bool BelongsToMonth(PersonRequest c, string monthKey)
    {
        if (c.Destination is not (CaseDestination.F8 or CaseDestination.UltimaCarpeta))
        {
            return false;
        }

        if (c.ClosedWithoutFolderAt is not null || c.SinCarpeta)
        {
            return false;
        }

        if (c.Status is not (RequestStatus.Uploaded or RequestStatus.Confirmed) || c.FechaUltimaCarpeta is null)
        {
            return false;
        }

        var uploaded = c.UploadedAt ?? c.ConfirmedAt;
        return uploaded is { } at && MonthKey(at) == monthKey;
    }

    /// <summary>The reminder shows on days 1-10 while a case of the previous month is still unmarked.</summary>
    public static bool ShouldRemind(IEnumerable<PersonRequest> cases, DateTimeOffset now)
    {
        if (now.LocalDateTime.Day > ReminderLastDay)
        {
            return false;
        }

        var previous = PreviousMonthKey(now);
        return cases.Any(c => c.MonthlyRequestPrintedAt is null && BelongsToMonth(c, previous));
    }
}
