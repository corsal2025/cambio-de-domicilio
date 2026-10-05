using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

public class ManualModel(IPersonRequestRepository repository) : PageModel
{
    public int TotalCasos { get; private set; }
    public int CasosEnCaja { get; private set; }
    public int CasosSinCarpeta { get; private set; }

    public void OnGet()
    {
        var all = repository.GetAll();
        TotalCasos = all.Count;
        CasosEnCaja = all.Count(c => c.Destination == Domain.CaseDestination.Caja);
        CasosSinCarpeta = all.Count(c => c.ClosedWithoutFolderAt is not null);
    }
}
