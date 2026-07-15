using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Extraction;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Routing;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class IndexModel(
    IPersonRequestRepository repository,
    IDiscardedEmailRepository discardedRepository,
    AddressChangeRoutingService routingService,
    RouterWorker routerWorker,
    RouterOptions options,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public IReadOnlyList<ComunaContact> ComunaOptions { get; private set; } = [];
    public int NeedsReviewCount { get; private set; }
    public int DiscardedCount { get; private set; }
    public string? StatusFilter { get; set; }
    public bool OnlyNeedsReview { get; set; }
    public string? SearchQuery { get; set; }
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }
    public int PlazoDiasHabiles => options.PlazoDiasHabiles;
    public bool AllVisibleMarked => Cases.Count > 0 && Cases.All(c => c.Marked);

    public void OnGet(string? status, bool needsReview = false, string? search = null)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        SearchQuery = search;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        logger.LogInformation("OnPostSetFecha caso={Id} valorRecibido='{Fecha}'", id, fecha);
        if (string.IsNullOrWhiteSpace(fecha))
        {
            repository.ClearFechaUltimaCarpeta(id);
            return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview });
        }

        if (!SpanishDate.TryParse(fecha, out var parsed))
        {
            Message = "Fecha no reconocida. Formatos aceptados: 15/03/2024 o 15 marzo 2024.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview });
    }

    public IActionResult OnPostSetPersonData(long id, string nombre, string rut)
    {
        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        // Uppercase to match the casing PersonDataExtractor already uses for auto-extracted names,
        // so manually-corrected cases don't end up in a different case than the rest of the report.
        nombre = (nombre ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedRut is null || nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            Message = normalizedRut is null
                ? "El RUT ingresado no es válido (revise el dígito verificador)."
                : "Ingrese el nombre completo (al menos nombre y apellido).";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetPersonData(id, nombre, normalizedRut);
        Message = "Datos guardados. El caso ya no requiere revisión.";
        Load();
        return Page();
    }

    /// <summary>Manually registers a case that didn't arrive by tracked email (e.g. a request
    /// received by phone, or an email that got missed) — the comuna must already be in the
    /// directory, since the confirmation step later resolves the contact address by exact name.</summary>
    public IActionResult OnPostAddManualCase(string comuna, string nombre, string rut)
    {
        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        nombre = (nombre ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedRut is null || nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            Message = normalizedRut is null
                ? "El RUT ingresado no es válido (revise el dígito verificador)."
                : "Ingrese el nombre completo (al menos nombre y apellido).";
            MessageIsError = true;
            Load();
            return Page();
        }

        var matchedComuna = routingService.LoadDirectory()
            .FirstOrDefault(c => string.Equals(c.Comuna, comuna, StringComparison.OrdinalIgnoreCase));
        if (matchedComuna is null)
        {
            Message = "La comuna ingresada no está en el directorio. Agréguela primero en la página 'Comunas'.";
            MessageIsError = true;
            Load();
            return Page();
        }

        if (repository.FindByRutAndComuna(normalizedRut, matchedComuna.Comuna) is not null)
        {
            Message = "Ya existe un caso registrado para esta persona y esta comuna.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.Insert(new PersonRequest
        {
            FullName = nombre,
            Rut = normalizedRut,
            Comuna = matchedComuna.Comuna,
            SourceMessageId = $"manual-{Guid.NewGuid()}",
            SourceSubject = "Ingresado manualmente por el operador",
            SourceSender = User.Identity?.Name ?? "operador",
            NeedsReview = false,
            Status = RequestStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow
        });

        Message = "Caso agregado manualmente.";
        Load();
        return Page();
    }

    /// <summary>Registers several manually-entered cases under one shared comuna in a single
    /// submission (e.g. a batch of requests received by phone for the same municipality). All
    /// rows are validated first; if any row fails, nothing is inserted — an all-or-nothing batch,
    /// same as if the operator had submitted <see cref="OnPostAddManualCase"/> once per row but
    /// without leaving partial data behind on a mid-batch mistake.</summary>
    public IActionResult OnPostAddManualCases(string comuna, List<string> nombre, List<string> rut)
    {
        var matchedComuna = routingService.LoadDirectory()
            .FirstOrDefault(c => string.Equals(c.Comuna, comuna, StringComparison.OrdinalIgnoreCase));
        if (matchedComuna is null)
        {
            Message = "La comuna ingresada no está en el directorio. Agréguela primero en la página 'Comunas'.";
            MessageIsError = true;
            Load();
            return Page();
        }

        var rows = nombre.Zip(rut, (n, r) => (Nombre: n, Rut: r))
            .Where(row => !string.IsNullOrWhiteSpace(row.Nombre) || !string.IsNullOrWhiteSpace(row.Rut))
            .ToList();

        if (rows.Count == 0)
        {
            Message = "Ingrese al menos un contribuyente.";
            MessageIsError = true;
            Load();
            return Page();
        }

        var toInsert = new List<PersonRequest>();
        var errors = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var normalizedRut = RutValidator.NormalizeAndValidate(rows[i].Rut);
            var nombreNormalizado = (rows[i].Nombre ?? string.Empty).Trim().ToUpperInvariant();

            if (normalizedRut is null || nombreNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            {
                errors.Add($"Fila {i + 1}: " + (normalizedRut is null
                    ? "RUT no válido (revise el dígito verificador)."
                    : "ingrese el nombre completo (al menos nombre y apellido)."));
                continue;
            }

            if (repository.FindByRutAndComuna(normalizedRut, matchedComuna.Comuna) is not null
                || toInsert.Any(p => p.Rut == normalizedRut))
            {
                errors.Add($"Fila {i + 1}: ya existe un caso registrado para esta persona y esta comuna.");
                continue;
            }

            toInsert.Add(new PersonRequest
            {
                FullName = nombreNormalizado,
                Rut = normalizedRut,
                Comuna = matchedComuna.Comuna,
                SourceMessageId = $"manual-{Guid.NewGuid()}",
                SourceSubject = "Ingresado manualmente por el operador",
                SourceSender = User.Identity?.Name ?? "operador",
                NeedsReview = false,
                Status = RequestStatus.Pending,
                ReceivedAt = DateTimeOffset.UtcNow
            });
        }

        if (errors.Count > 0)
        {
            Message = string.Join(" ", errors);
            MessageIsError = true;
            Load();
            return Page();
        }

        foreach (var request in toInsert)
        {
            repository.Insert(request);
        }

        Message = toInsert.Count == 1
            ? "Caso agregado manualmente."
            : $"{toInsert.Count} casos agregados manualmente.";
        Load();
        return Page();
    }

    /// <summary>Operator-triggered permanent removal of a case (e.g. a mistaken manual entry, or
    /// one that should never have been tracked) — unlike the automatic revert-to-Pending on a
    /// re-found source email, this actually erases the row.</summary>
    public IActionResult OnPostDeleteCase(long id)
    {
        // Tombstone the source email BEFORE deleting the row (need it while the row still
        // exists) so a future sync cycle never recreates this case from the same email.
        var sourceMessageId = repository.FindById(id)?.SourceMessageId;
        repository.Delete(id);
        if (sourceMessageId is not null)
        {
            repository.RecordDeletedSourceMessage(sourceMessageId);
        }

        Message = "Caso eliminado.";
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery });
    }

    public IActionResult OnPostToggleMarked(long id, string? markedValue)
    {
        // Only accept explicit 'on' value (checkbox form submission), reject anything else
        var marked = markedValue == "on";
        repository.SetMarked(id, marked);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery });
    }

    public IActionResult OnPostToggleFolderNotFound(long id, string? folderNotFoundValue)
    {
        var folderNotFound = folderNotFoundValue == "on";
        repository.SetFolderNotFound(id, folderNotFound);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery });
    }

    /// <summary>Exports every "F8" case currently marked (<see cref="PersonRequest.FolderNotFound"/>)
    /// to an .xlsx workbook, regardless of whether it has already been notified — this lets the
    /// operator hand the list to Secretaría Municipal for the physical certificate request.</summary>
    public IActionResult OnGetExportFolderNotFound()
    {
        var cases = repository.GetAll()
            .Where(c => c.FolderNotFound)
            .OrderBy(c => c.Comuna)
            .ThenBy(c => c.FullName)
            .ToList();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("F8");
        sheet.Cell(1, 1).Value = "Nombre";
        sheet.Cell(1, 2).Value = "RUT";
        sheet.Cell(1, 3).Value = "Comuna";
        sheet.Cell(1, 4).Value = "Fecha última carpeta";
        sheet.Cell(1, 5).Value = "Avisado";
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in cases)
        {
            sheet.Cell(row, 1).Value = item.FullName ?? string.Empty;
            sheet.Cell(row, 2).Value = item.Rut ?? string.Empty;
            sheet.Cell(row, 3).Value = item.Comuna ?? string.Empty;
            sheet.Cell(row, 4).Value = item.FechaUltimaCarpeta?.ToString("dd-MM-yyyy") ?? "sin fecha";
            sheet.Cell(row, 5).Value = item.FolderNotFoundNotifiedAt is not null ? "Sí" : "No";
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"F8_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Marks (or unmarks) every case currently visible under the active filter — a
    /// bulk shortcut for the per-row "Marcar" checkbox, respecting the same status/needsReview
    /// filter the operator is looking at.</summary>
    public IActionResult OnPostMarkAllVisible(bool marked, string? status, bool needsReview, string? search)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        SearchQuery = search;
        Load();
        foreach (var item in Cases)
        {
            repository.SetMarked(item.Id, marked);
        }

        return RedirectToPage(new { status, needsReview, search });
    }

    /// <summary>Selects exactly the still-outstanding cases in the given sector (derived from
    /// Fecha última carpeta) — marks every matching, not-yet-Confirmed case AND unmarks every
    /// other one, regardless of the current filter, so the checked set on screen always reflects
    /// only that sector's pending work. Confirmed (blue) cases are excluded: they're already
    /// done, selecting them again for a new PDF run makes no sense. A shortcut so the operator
    /// doesn't have to hunt down and tick every Archivo (or Oficina 43) case one by one before
    /// generating that sector's PDF.</summary>
    public IActionResult OnPostMarkAllBySector(FolderSector sector, string? status, bool needsReview, string? search)
    {
        foreach (var item in repository.GetAll())
        {
            repository.SetMarked(item.Id, item.Sector == sector && item.Status != RequestStatus.Confirmed);
        }

        return RedirectToPage(new { status, needsReview, search });
    }

    public async Task<IActionResult> OnPostSyncNowAsync()
    {
        var ran = await routerWorker.RunCycleAsync(HttpContext.RequestAborted);
        Message = ran
            ? "Sincronización completada."
            : "Ya hay una sincronización en curso, intente en unos segundos.";
        MessageIsError = !ran;
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var contacts = routingService.LoadDirectory();
        var result = await routingService.SendConfirmationAsync(id, userId, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>One-click action for a Pending case: moves the original email to "ya subida" and
    /// sends the confirmation, in one step (see AddressChangeRoutingService.MarkUploadedAndConfirmAsync).</summary>
    public async Task<IActionResult> OnPostMarkUploadedAndConfirmAsync(long id)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var contacts = routingService.LoadDirectory();
        var result = await routingService.MarkUploadedAndConfirmAsync(id, userId, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Undo for a Confirmed case clicked by mistake: sends a rectification email to the
    /// comuna and reverts the case to Pending — see AddressChangeRoutingService.RectifyConfirmationAsync.</summary>
    public async Task<IActionResult> OnPostRectifyConfirmationAsync(long id)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var contacts = routingService.LoadDirectory();
        var result = await routingService.RectifyConfirmationAsync(id, userId, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Business days remaining until the legal upload deadline for this case, from today.</summary>
    public int DiasHabilesRestantes(PersonRequest request)
    {
        var received = DateOnly.FromDateTime(request.ReceivedAt.LocalDateTime);
        var deadline = DeadlineCalculator.AddBusinessDays(received, options.PlazoDiasHabiles);
        return DeadlineCalculator.BusinessDaysRemaining(DateOnly.FromDateTime(DateTime.Today), deadline);
    }

    private void Load()
    {
        var everything = repository.GetAll();
        NeedsReviewCount = everything.Count(c => c.NeedsReview);
        DiscardedCount = discardedRepository.GetAll().Count;
        ComunaOptions = routingService.LoadDirectory()
            .DistinctBy(c => c.Comuna, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Comuna)
            .ToList();

        var all = everything.AsEnumerable();

        if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<RequestStatus>(StatusFilter, out var status))
        {
            all = all.Where(c => c.Status == status);
        }

        if (OnlyNeedsReview)
        {
            all = all.Where(c => c.NeedsReview);
        }

        // Search by name or RUT (case-insensitive, partial match)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim().ToUpperInvariant();
            all = all.Where(c => 
                (c.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (c.Rut ?? string.Empty).Replace(".", string.Empty).Replace("-", string.Empty)
                    .Contains(query.Replace(".", string.Empty).Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)
            );
        }

        // Confirmed cases (blue row — folder uploaded, comuna already emailed) sink to the very
        // end, ordered by ConfirmedAt ascending, so confirmations show in the order they
        // happened instead of mixed in with outstanding work. The Marcar checkbox is pure
        // personal bookkeeping (see PersonRequest.Marked) and does NOT affect sort order —
        // ticking it just highlights the row, it never moves. Everything not Confirmed stays on
        // top, ordered by ReceivedAt (fecha de ingreso — when the email actually arrived), not by
        // CreatedAt (when the row was inserted): a case re-tracked later (e.g. after being
        // reverted from Uploaded back to Pending) must stay in its original position instead of
        // jumping to the top just because its database row is newer.
        Cases = all
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();
    }
}
