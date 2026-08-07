using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Statistics;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Read-only visual overview of the whole system — Casos, F8, Certificado, and Discarded —
/// built entirely from data every other screen already stores. No writes happen here.</summary>
[Authorize]
public class EstadisticasModel(
    IPersonRequestRepository repository,
    IDiscardedEmailRepository discardedRepository,
    StatisticsService statisticsService) : PageModel
{
    public StatusCounts StatusCounts { get; private set; } = new(0, 0, 0);
    public IReadOnlyList<WeeklyIntakePoint> WeeklyIntake { get; private set; } = [];
    public IReadOnlyList<ComunaCount> TopComunas { get; private set; } = [];
    public IReadOnlyList<ReasonCount> DiscardedByReason { get; private set; } = [];
    public TurnaroundResult Turnaround { get; private set; } = new(false, 0);
    public IReadOnlyList<TurnaroundPoint> TurnaroundDistribution { get; private set; } = [];
    public SectorDistribution SectorDistribution { get; private set; } = new(0, 0);
    public F8DeadlineBacklog F8DeadlineBacklog { get; private set; } = new(0, 0);
    public F8PdfStatus F8PdfStatus { get; private set; } = new(0, 0);
    public CertificadoFolderStatus CertificadoFolderStatus { get; private set; } = new(0, 0);
    public CertificadoNotificationStatus CertificadoNotificationStatus { get; private set; } = new(0, 0);

    public void OnGet()
    {
        var cases = repository.GetAll();
        var discarded = discardedRepository.GetAll();

        StatusCounts = statisticsService.GetStatusCounts(cases);
        WeeklyIntake = statisticsService.GetWeeklyIntake(cases);
        TopComunas = statisticsService.GetTopComunas(cases);
        DiscardedByReason = statisticsService.GetDiscardedByReason(discarded);
        Turnaround = statisticsService.GetAverageTurnaroundDays(cases);
        TurnaroundDistribution = statisticsService.GetTurnaroundDistribution(cases);
        SectorDistribution = statisticsService.GetSectorDistribution(cases);
        F8DeadlineBacklog = statisticsService.GetF8DeadlineBacklog(cases);
        F8PdfStatus = statisticsService.GetF8PdfStatus(cases);
        CertificadoFolderStatus = statisticsService.GetCertificadoFolderStatus(cases);
        CertificadoNotificationStatus = statisticsService.GetCertificadoNotificationStatus(cases);
    }
}
