using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>
/// "Pedido mensual": the printable monthly list of F8 folders to request, the counterpart of the
/// "Impresión mensual" of F8 Urgentes. It never sends an email; marking it only stamps the cases so the
/// reminder on F8 and Casos goes away.
/// </summary>
public class PedidoMensualModel(IPersonRequestRepository repository, TimeProvider? timeProvider = null) : PageModel
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>The month being shown, as yyyy-MM.</summary>
    public string Month { get; private set; } = string.Empty;

    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];

    /// <summary>How many of the listed cases are already marked as requested.</summary>
    public int PrintedCount => Cases.Count(c => c.MonthlyRequestPrintedAt is not null);

    public string Folio { get; private set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>Spanish month name and year of <see cref="Month"/>, e.g. "septiembre 2026".</summary>
    public string MonthLabel { get; private set; } = string.Empty;

    public void OnGet(string? month = null)
    {
        Load(ResolveMonth(month));
    }

    /// <summary>"Marcar como pedido": stamps every case of the month. They stay in the listing.</summary>
    public IActionResult OnPostMarkPrinted(string? month)
    {
        var resolved = ResolveMonth(month);
        var ids = CasesFor(resolved).Select(c => c.Id).ToList();
        repository.SetMonthlyRequestPrinted(ids, clock.GetUtcNow());
        return RedirectToPage(new { month = resolved });
    }

    private string ResolveMonth(string? month) =>
        MonthlyFolderRequest.IsValidMonthKey(month) ? month! : MonthlyFolderRequest.PreviousMonthKey(clock.GetLocalNow());

    private IReadOnlyList<PersonRequest> CasesFor(string month) =>
        repository.Find(new CaseQuery { Destinations = [CaseDestination.F8, CaseDestination.UltimaCarpeta] })
            .Where(c => MonthlyFolderRequest.BelongsToMonth(c, month))
            .OrderBy(c => c.UploadedAt ?? c.ConfirmedAt)
            .ThenBy(c => c.Id)
            .ToList();

    private void Load(string month)
    {
        Month = month;
        Cases = CasesFor(month);
        GeneratedAt = clock.GetLocalNow();
        Folio = $"F8-Mensual-{month.Replace("-", string.Empty)}-{GeneratedAt:HHmm}";
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        MonthLabel = SpanishDate.Format(first).Split(' ')[1] + " " + first.Year;
    }
}
