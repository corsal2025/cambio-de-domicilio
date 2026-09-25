using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Tracks which physical box each uploaded folder ends up packed into. Cases land here
/// when the operator presses "Caja" in Casos or F8. "Cerrar Caja"
/// snapshots everything currently queued into a new, sequentially-numbered box; the next case to
/// arrive starts filling the next one. F8 cases never appear here — see PersonRequest.BoxId.</summary>
[IgnoreAntiforgeryToken]
public class CajaModel(IPersonRequestRepository repository) : PageModel
{
    public IReadOnlyList<PersonRequest> Queue { get; private set; } = [];
    public IReadOnlyList<Box> ClosedBoxes { get; private set; } = [];
    public Dictionary<long, int> BoxCounts { get; private set; } = [];
    public int NextBoxNumber { get; private set; } = 1;

    /// <summary>Set when viewing a single closed box's printable detail (?boxId=N).</summary>
    public Box? SelectedBox { get; private set; }
    public IReadOnlyList<PersonRequest> SelectedBoxCases { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    public void OnGet(long? boxId)
    {
        Load(boxId);
    }

    /// <summary>Closes the current queue into a new box with the operator's manual code (e.g. A1-CD),
    /// then goes straight to that box's printable detail so the operator can print/save it right away.</summary>
    public IActionResult OnPostCerrarCaja([FromForm] string? boxNumber, [FromForm] string? boxCode)
    {
        var existingBoxes = repository.GetBoxes();
        var nextNum = existingBoxes.Count > 0 ? existingBoxes.Max(b => b.Number) + 1 : 1;
        var raw = !string.IsNullOrWhiteSpace(boxNumber) ? boxNumber : boxCode;
        var normalizedCode = FormatBoxCode(raw, nextNum);

        // Reusing an existing code is allowed on purpose: several closes can go into the same
        // physical box (e.g. three batches all packed into box A1-CD).

        var box = repository.CloseBox(normalizedCode, DateTimeOffset.UtcNow);
        Message = $"Caja {box.Code} cerrada exitosamente.";
        return RedirectToPage(new { boxId = box.Id });
    }

    /// <summary>Reopens a closed box, sending all its folders back to the open queue so the operator can adjust it.</summary>
    public IActionResult OnPostReopenBox([FromForm] long boxId)
    {
        var box = repository.FindBoxById(boxId);
        var code = box?.Code ?? $"#{boxId}";
        repository.ReopenBox(boxId);
        Message = $"Caja {code} reabierta con éxito. Sus carpetas volvieron a la cola para que puedas quitar las que sobren o corregirlas.";
        return RedirectToPage("/Caja", new { boxId = (long?)null });
    }

    /// <summary>Removes an individual folder from an already closed box and returns it back to Casos.</summary>
    public IActionResult OnPostRemoveFromClosedBox([FromForm] long id, [FromForm] long boxId)
    {
        repository.RemoveCaseFromClosedBox(id);
        Message = "Carpeta quitada de la caja y devuelta a Casos.";
        return RedirectToPage(new { boxId });
    }

    /// <summary>Removes a single folder from the Caja queue and returns it to Casos (Destination = None).</summary>
    public IActionResult OnPostUndoSingle(long id)
    {
        repository.ClearDestination(id);
        Message = "Carpeta quitada de la cola de caja y devuelta a Casos.";
        return RedirectToPage();
    }

    /// <summary>Removes multiple selected folders from the Caja queue and returns them to Casos.</summary>
    public IActionResult OnPostUndoBatch([FromForm] List<long> selectedIds)
    {
        if (selectedIds is { Count: > 0 })
        {
            foreach (var id in selectedIds)
            {
                repository.ClearDestination(id);
            }
            Message = $"{selectedIds.Count} carpeta(s) quitadas de la cola de caja y devueltas a Casos.";
        }
        return RedirectToPage();
    }

    public static string FormatBoxCode(string? boxNumberOrCode, int defaultNumber)
    {
        if (string.IsNullOrWhiteSpace(boxNumberOrCode))
            return $"A{defaultNumber}-CD";

        var trimmed = boxNumberOrCode.Trim().ToUpperInvariant();
        if (trimmed.EndsWith("-CD", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^3].Trim();
        else if (trimmed.EndsWith("CD", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^2].Trim();

        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([A-Z]*)\s*(\d+)$");
        if (match.Success)
        {
            var prefix = string.IsNullOrEmpty(match.Groups[1].Value) ? "A" : match.Groups[1].Value;
            var num = match.Groups[2].Value;
            return $"{prefix}{num}-CD";
        }

        if (int.TryParse(trimmed, out var n))
            return $"A{n}-CD";

        return $"{trimmed}-CD";
    }

    private void Load(long? boxId)
    {
        Queue = repository.GetCajaQueue();
        ClosedBoxes = repository.GetBoxes();
        NextBoxNumber = ClosedBoxes.Count > 0 ? ClosedBoxes.Max(b => b.Number) + 1 : 1;
        BoxCounts = ClosedBoxes.ToDictionary(b => b.Id, b => repository.GetCasesByBoxId(b.Id).Count);

        if (boxId is { } id)
        {
            SelectedBox = repository.FindBoxById(id);
            SelectedBoxCases = SelectedBox is null ? [] : repository.GetCasesByBoxId(id);
        }
    }
}
