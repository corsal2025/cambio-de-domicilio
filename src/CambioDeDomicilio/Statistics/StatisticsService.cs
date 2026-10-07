using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Statistics;

public sealed record StatusCounts(int Pending, int Uploaded, int Confirmed);

public sealed record WeeklyIntakePoint(DateOnly WeekStart, int Count);

public sealed record ComunaCount(string Comuna, int Count);

public sealed record ReasonCount(string Reason, int Count);

public sealed record TurnaroundResult(bool HasData, double AverageDays);

/// <summary>One Confirmed case's turnaround, for the dispersion scatter chart — ReceivedAt is the
/// x-axis (when it came in) so the operator can spot whether slow cases cluster around a
/// particular period, not just see a single averaged number.</summary>
public sealed record TurnaroundPoint(DateOnly ReceivedAt, double Days);

public sealed record SectorDistribution(int Archivo, int Oficina43);

public sealed record F8DeadlineBacklog(int WithinDeadline, int PastDeadline);

public sealed record F8PdfStatus(int Generated, int Pending);

/// <summary>Read-only aggregations over already-loaded case/discarded-email lists, feeding the
/// /Estadisticas dashboard. Mirrors the in-memory LINQ pattern every other dashboard page already
/// uses over IPersonRequestRepository.GetAll() — whole-table by nature, so it stays on GetAll.</summary>
public sealed class StatisticsService(RouterOptions options, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public StatusCounts GetStatusCounts(IReadOnlyList<PersonRequest> cases) => new(
        Pending: cases.Count(c => c.Status == RequestStatus.Pending),
        Uploaded: cases.Count(c => c.Status == RequestStatus.Uploaded),
        Confirmed: cases.Count(c => c.Status == RequestStatus.Confirmed));

    /// <summary>Buckets by the Monday of each ISO week, filling in zero-count weeks between the
    /// earliest and latest ReceivedAt so the trend line doesn't silently skip quiet weeks.</summary>
    public IReadOnlyList<WeeklyIntakePoint> GetWeeklyIntake(IReadOnlyList<PersonRequest> cases)
    {
        if (cases.Count == 0)
        {
            return [];
        }

        static DateOnly WeekStart(DateTimeOffset receivedAt)
        {
            var date = DateOnly.FromDateTime(receivedAt.UtcDateTime);
            var offset = ((int)date.DayOfWeek + 6) % 7; // days since Monday
            return date.AddDays(-offset);
        }

        var counts = cases
            .GroupBy(c => WeekStart(c.ReceivedAt))
            .ToDictionary(g => g.Key, g => g.Count());

        var firstWeek = counts.Keys.Min();
        var lastWeek = counts.Keys.Max();

        var points = new List<WeeklyIntakePoint>();
        for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
        {
            points.Add(new WeeklyIntakePoint(week, counts.GetValueOrDefault(week, 0)));
        }

        return points;
    }

    public IReadOnlyList<ComunaCount> GetTopComunas(IReadOnlyList<PersonRequest> cases) => cases
        .Where(c => !string.IsNullOrWhiteSpace(c.Comuna))
        .GroupBy(c => c.Comuna!)
        .Select(g => new ComunaCount(g.Key, g.Count()))
        .OrderByDescending(r => r.Count)
        .ThenBy(r => r.Comuna, StringComparer.OrdinalIgnoreCase)
        .Take(10)
        .ToList();

    public IReadOnlyList<ReasonCount> GetDiscardedByReason(IReadOnlyList<DiscardedEmail> emails) => emails
        .GroupBy(e => e.Reason)
        .Select(g => new ReasonCount(g.Key, g.Count()))
        .OrderByDescending(r => r.Count)
        .ThenBy(r => r.Reason, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>Only Status == Confirmed cases have both ReceivedAt and ConfirmedAt meaningfully
    /// set — Pending/Uploaded cases are excluded rather than estimated (see design.md decision 5).</summary>
    public TurnaroundResult GetAverageTurnaroundDays(IReadOnlyList<PersonRequest> cases)
    {
        var confirmed = cases.Where(c => c.Status == RequestStatus.Confirmed && c.ConfirmedAt is not null).ToList();
        if (confirmed.Count == 0)
        {
            return new TurnaroundResult(HasData: false, AverageDays: 0);
        }

        var average = confirmed.Average(c => (c.ConfirmedAt!.Value - c.ReceivedAt).TotalDays);
        return new TurnaroundResult(HasData: true, AverageDays: average);
    }

    /// <summary>One point per Confirmed case, ordered by ReceivedAt — the scatter/dispersion view
    /// behind the single averaged number GetAverageTurnaroundDays returns, so the operator can see
    /// whether turnaround is consistent or has outliers/clusters.</summary>
    public IReadOnlyList<TurnaroundPoint> GetTurnaroundDistribution(IReadOnlyList<PersonRequest> cases) => cases
        .Where(c => c.Status == RequestStatus.Confirmed && c.ConfirmedAt is not null)
        .Select(c => new TurnaroundPoint(
            DateOnly.FromDateTime(c.ReceivedAt.UtcDateTime),
            (c.ConfirmedAt!.Value - c.ReceivedAt).TotalDays))
        .OrderBy(p => p.ReceivedAt)
        .ToList();

    public SectorDistribution GetSectorDistribution(IReadOnlyList<PersonRequest> cases) => new(
        Archivo: cases.Count(c => c.Sector == FolderSector.Archivo),
        Oficina43: cases.Count(c => c.Sector == FolderSector.Oficina43));

    /// <summary>Same within/past-deadline rule the /F8 screen already applies per row
    /// (F8.cshtml.cs's "restantes" via DeadlineCalculator + RouterOptions.PlazoDiasHabiles).</summary>
    public F8DeadlineBacklog GetF8DeadlineBacklog(IReadOnlyList<PersonRequest> cases)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var f8Cases = cases.Where(c => c.Destination == CaseDestination.F8).ToList();

        var withinDeadline = 0;
        var pastDeadline = 0;
        foreach (var c in f8Cases)
        {
            var received = DateOnly.FromDateTime(c.ReceivedAt.ToLocalTime().DateTime); // same day the F8 badge uses
            var deadline = DeadlineCalculator.AddBusinessDays(received, options.PlazoDiasHabiles);
            var remaining = DeadlineCalculator.BusinessDaysRemaining(today, deadline);
            if (remaining < 0)
            {
                pastDeadline++;
            }
            else
            {
                withinDeadline++;
            }
        }

        return new F8DeadlineBacklog(withinDeadline, pastDeadline);
    }

    public F8PdfStatus GetF8PdfStatus(IReadOnlyList<PersonRequest> cases)
    {
        var withSector = cases.Where(c => c.Destination == CaseDestination.F8 && c.Sector is not null).ToList();
        return new F8PdfStatus(
            Generated: withSector.Count(c => c.SectorPdfGeneratedAt is not null),
            Pending: withSector.Count(c => c.SectorPdfGeneratedAt is null));
    }
}
