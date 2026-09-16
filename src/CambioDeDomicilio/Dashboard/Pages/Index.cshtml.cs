using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Extraction;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;

namespace CambioDeDomicilio.Dashboard.Pages;

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

    /// <summary>Confirmed cases whose confirmation email bounced (see <see cref="PersonRequest.ConfirmationBouncedAt"/>).</summary>
    public int BouncedCount { get; private set; }
    public string? StatusFilter { get; set; }
    public bool OnlyNeedsReview { get; set; }
    public bool OnlyBounced { get; set; }
    public string? SearchQuery { get; set; }

    /// <summary>Number of matching cases found in F8 for this search query.</summary>
    public int F8MatchCount { get; private set; }
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }
    public int PlazoDiasHabiles => options.PlazoDiasHabiles;
    public bool AllVisibleMarked => Cases.Count > 0 && Cases.All(c => c.Marked);

    public void OnGet(string? status, bool needsReview = false, string? search = null, bool bounced = false)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        OnlyBounced = bounced;
        SearchQuery = search;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        logger.LogInformation("OnPostSetFecha caso={Id} valorRecibido='{Fecha}'", id, fecha);
        if (string.IsNullOrWhiteSpace(fecha))
        {
            repository.ClearFechaUltimaCarpeta(id);
            return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
        }

        if (!SpanishDate.TryParse(fecha, out var parsed))
        {
            Message = "Fecha no reconocida. Formatos aceptados: 15/03/2024 o 15 marzo 2024.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
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

    public IActionResult OnPostTogglePendienteCarpeta(long id, string? pendienteCarpetaValue)
    {
        var pendienteCarpeta = pendienteCarpetaValue == "on";
        repository.SetPendienteCarpeta(id, pendienteCarpeta);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery });
    }

    /// <summary>Operator-confirmed move to F8: the case disappears from Casos and starts showing
    /// in the F8 page. Ticking the F8 checkbox alone (<see cref="OnPostToggleFolderNotFound"/>)
    /// does not do this by itself — it only marks the case as an F8 candidate.</summary>
    public IActionResult OnPostTransferToF8(long id)
    {
        // F8's "Fecha penúltima carpeta" is a distinct date from Casos' última carpeta — carrying
        // the old value over would read as already filled in, so it's cleared here and the
        // operator fills it in fresh on the F8 screen.
        repository.ClearFechaUltimaCarpeta(id);
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery });
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

    public async Task<IActionResult> OnPostSyncNowAsync()
    {
        // Not HttpContext.RequestAborted: a slow cycle (many inbox messages during the bounce
        // check) can outlive the browser request. Tying it to the request token meant a closed
        // tab or proxy timeout aborted the EWS calls mid-cycle, so the sync appeared to silently
        // fail even though nothing was actually broken.
        var result = await routerWorker.RunCycleAsync(CancellationToken.None);
        if (result.AlreadyRunning)
        {
            Message = "Ya hay una sincronización en curso, intente en unos segundos.";
            MessageIsError = true;
        }
        else if (!result.Success)
        {
            Message = result.ErrorMessage ?? "Error al sincronizar con el servidor de correo.";
            MessageIsError = true;
        }
        else
        {
            Message = "Sincronización completada.";
            MessageIsError = false;
        }
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.SendConfirmationAsync(id, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>One-click action for a Pending case: moves the original email to "ya subida" and
    /// sends the confirmation, in one step (see AddressChangeRoutingService.MarkUploadedAndConfirmAsync).</summary>
    public async Task<IActionResult> OnPostMarkUploadedAndConfirmAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.MarkUploadedAndConfirmAsync(id, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Undo for a Confirmed case clicked by mistake: sends a rectification email to the
    /// comuna and reverts the case to Pending — see AddressChangeRoutingService.RectifyConfirmationAsync.</summary>
    public async Task<IActionResult> OnPostRectifyConfirmationAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.RectifyConfirmationAsync(id, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Operator "Marcar resuelto" on a bounced confirmation: clears the bounce flag once
    /// they have re-sent the confirmation or handled the non-delivery another way.</summary>
    public IActionResult OnPostResolveBounce(long id)
    {
        repository.ClearConfirmationBounced(id);
        Message = "Rebote marcado como resuelto.";
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
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
        BouncedCount = everything.Count(c => c.ConfirmationBouncedAt is not null);
        DiscardedCount = discardedRepository.GetAll().Count;
        ComunaOptions = routingService.LoadDirectory()
            .DistinctBy(c => c.Comuna, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Comuna)
            .ToList();

        var all = everything.AsEnumerable();

        // Cases already transferred to F8 (see OnPostTransferToF8) live on the dedicated F8 page
        // instead — ticking the F8 checkbox alone does not remove a case from here.
        all = all.Where(c => c.TransferredAt is null);

        if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<RequestStatus>(StatusFilter, out var status))
        {
            all = all.Where(c => c.Status == status);
        }

        if (OnlyNeedsReview)
        {
            all = all.Where(c => c.NeedsReview);
        }

        if (OnlyBounced)
        {
            all = all.Where(c => c.ConfirmationBouncedAt is not null);
        }

        // Search by name or RUT (case-insensitive, partial match)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim().ToUpperInvariant();
            all = all.Where(c => MatchesQuery(c, query));

            // Conteo de coincidencias cruzadas en F8
            F8MatchCount = everything.Count(c => c.Destination == CaseDestination.F8 && MatchesQuery(c, query));
        }

        // Marked cases (checkbox "Marcar") float to the very top, ordered by MarkedAt ascending —
        // the order the operator ticked them in — so the marked set on screen lines up with the
        // order they'll print in on the next PDF run (see SetMarked/Sector.cshtml.cs). Confirmed
        // cases (blue row — folder uploaded, comuna already emailed) sink to the very end, ordered
        // by ConfirmedAt ascending, so confirmations show in the order they happened instead of
        // mixed in with outstanding work. Everything else stays in the middle, ordered by
        // ReceivedAt (fecha de ingreso — when the email actually arrived), not by CreatedAt (when
        // the row was inserted): a case re-tracked later (e.g. after being reverted from Uploaded
        // back to Pending) must stay in its original position instead of jumping to the top just
        // because its database row is newer.
        Cases = all
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenByDescending(c => c.Marked && c.SectorPdfGeneratedAt is null)
            .ThenByDescending(c => c.Marked)
            .ThenBy(c => c.MarkedAt)
            .ThenBy(c => c.FechaUltimaCarpeta is not null)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();
    }

    private static bool MatchesQuery(PersonRequest c, string query)
    {
        var queryClean = query.Replace(".", string.Empty).Replace("-", string.Empty);
        var rutClean = (c.Rut ?? string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);
        return (c.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
               rutClean.Contains(queryClean, StringComparison.OrdinalIgnoreCase);
    }
}
