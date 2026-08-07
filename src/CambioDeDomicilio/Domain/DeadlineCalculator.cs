namespace CambioDeDomicilio.Domain;

/// <summary>
/// Legal-deadline math in business days (Mon–Fri). Chilean public holidays are NOT
/// excluded — the countdown is slightly conservative on weeks with holidays, which
/// errs on the safe side for a legal deadline.
/// </summary>
public static class DeadlineCalculator
{
    public static DateOnly AddBusinessDays(DateOnly start, int businessDays)
    {
        var date = start;
        var added = 0;
        while (added < businessDays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }
        return date;
    }

    /// <summary>Business days from <paramref name="today"/> until <paramref name="deadline"/>; negative when overdue.</summary>
    public static int BusinessDaysRemaining(DateOnly today, DateOnly deadline)
    {
        if (today == deadline)
        {
            return 0;
        }

        var sign = today < deadline ? 1 : -1;
        var (from, to) = today < deadline ? (today, deadline) : (deadline, today);
        var count = 0;
        var date = from;
        while (date < to)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
        }
        return sign * count;
    }
}
