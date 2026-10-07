using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Persistence;

/// <summary>Filters the dashboard pages push down to SQL instead of loading the whole table.
/// Every property is optional and they combine with AND; a null property does not filter.
/// Results are always ordered by Id, the same order <c>GetAll()</c> returns, so a page that
/// sorts the rows afterwards (LINQ <c>OrderBy</c> is stable) keeps its current ordering of ties.</summary>
public sealed record CaseQuery
{
    /// <summary>Any of these destinations. An empty list matches nothing.</summary>
    public IReadOnlyList<CaseDestination>? Destinations { get; init; }

    /// <summary>Any of these statuses. An empty list matches nothing.</summary>
    public IReadOnlyList<RequestStatus>? Statuses { get; init; }

    public bool? NeedsReview { get; init; }

    /// <summary>True: the confirmation email bounced (ConfirmationBouncedAt set). False: it did not.</summary>
    public bool? Bounced { get; init; }

    public bool? Marked { get; init; }

    /// <summary>True: TransferredAt is set. False: it is not — independent of the destination column.</summary>
    public bool? Transferred { get; init; }

    /// <summary>Same rule as <see cref="PersonRequest.Sector"/>: cases without a last-folder date never match.</summary>
    public FolderSector? Sector { get; init; }

    /// <summary>True: the SinCarpetas page's definition — destination SinCarpetas, or closed without a
    /// folder, or flagged SinCarpeta — whatever the destination column says. False: none of those.</summary>
    public bool? InSinCarpetasBucket { get; init; }
}
