using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Tracks which physical box each uploaded folder ends up packed into. Cases land here
/// automatically the moment they're marked uploaded (see
/// AddressChangeRoutingService.MarkUploadedAndConfirmAsync) — no manual selection. "Cerrar Caja"
/// snapshots everything currently queued into a new, sequentially-numbered box; the next case to
/// arrive starts filling the next one. F8 cases never appear here — see PersonRequest.BoxId.</summary>
public class CajaModel(IPersonRequestRepository repository) : PageModel
{
    public IReadOnlyList<PersonRequest> Queue { get; private set; } = [];
    public IReadOnlyList<Box> ClosedBoxes { get; private set; } = [];

    /// <summary>Set when viewing a single closed box's printable detail (?boxId=N).</summary>
    public Box? SelectedBox { get; private set; }
    public IReadOnlyList<PersonRequest> SelectedBoxCases { get; private set; } = [];

    public void OnGet(long? boxId)
    {
        Load(boxId);
    }

    /// <summary>Closes the current queue into a new box, then goes straight to that box's
    /// printable detail so the operator can print/save it right away.</summary>
    public IActionResult OnPostCerrarCaja()
    {
        var box = repository.CloseBox(DateTimeOffset.UtcNow);
        return RedirectToPage(new { boxId = box.Id });
    }

    private void Load(long? boxId)
    {
        Queue = repository.GetCajaQueue();
        ClosedBoxes = repository.GetBoxes();

        if (boxId is { } id)
        {
            SelectedBox = repository.FindBoxById(id);
            SelectedBoxCases = SelectedBox is null ? [] : repository.GetCasesByBoxId(id);
        }
    }
}
