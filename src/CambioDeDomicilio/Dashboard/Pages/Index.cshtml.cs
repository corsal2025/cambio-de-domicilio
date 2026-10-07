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
    IMessageTombstoneRepository tombstones,
    IBoxRepository boxes,
    AddressChangeRoutingService routingService,
    RouterWorker routerWorker,
    RouterOptions options,
    ILogger<IndexModel> logger,
    TimeProvider? timeProvider = null) : PageModel
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public IReadOnlyList<ComunaContact> ComunaOptions { get; private set; } = [];
    public int NeedsReviewCount { get; private set; }
    public int DiscardedCount { get; private set; }

    /// <summary>Confirmed cases whose confirmation email bounced (see <see cref="PersonRequest.ConfirmationBouncedAt"/>).</summary>
    public int BouncedCount { get; private set; }
    // List state (filter + search) is bound on GET and on every POST, so each action's redirect
    // returns the operator to the same filtered list (the page injects these as hidden fields).
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "needsReview")]
    public bool OnlyNeedsReview { get; set; }

    [BindProperty(SupportsGet = true, Name = "bounced")]
    public bool OnlyBounced { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? SearchQuery { get; set; }

    /// <summary>Number of matching cases found in F8 for this search query.</summary>
    public int F8MatchCount { get; private set; }
    public long? F8FirstMatchId { get; private set; }
    public int CajaMatchCount { get; private set; }
    public string? CajaMatchBoxCode { get; private set; }
    public int SubidasMatchCount { get; private set; }

    /// <summary>Exact physical location of every searched case that is in Caja: which listing
    /// (box Id — codes can repeat), its close date, and the N° it has in that printed listing.</summary>
    public IReadOnlyList<CajaLocation> CajaMatches { get; private set; } = [];
    /// <summary>Result of the last action. Carried across a redirect through TempData (see
    /// <see cref="PersistMessageForRedirect"/>) and read once in OnGet.</summary>
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    private const string MessageKey = "Index.Message";
    private const string MessageIsErrorKey = "Index.MessageIsError";

    /// <summary>Only a redirect needs the message stored for the next request; a same-request
    /// Page() already renders it, and storing it too would show it again on the next load.</summary>
    public void PersistMessageForRedirect(IActionResult? result)
    {
        if (result is RedirectToPageResult && Message is not null && TempData is not null)
        {
            TempData[MessageKey] = Message;
            TempData[MessageIsErrorKey] = MessageIsError;
        }
    }

    public override void OnPageHandlerExecuted(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutedContext context) =>
        PersistMessageForRedirect(context.Result);
    public int PlazoDiasHabiles => options.PlazoDiasHabiles;
    public bool AllVisibleMarked => Cases.Count > 0 && Cases.All(c => c.Marked);

    public void OnGet(string? status, bool needsReview = false, string? search = null, bool bounced = false)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        OnlyBounced = bounced;
        SearchQuery = search;
        Message ??= TempData?[MessageKey] as string;
        if (TempData?[MessageIsErrorKey] is bool isError) MessageIsError = isError;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        var isAjax = string.Equals(HttpContext?.Request?.Headers?.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                     || (HttpContext?.Request?.Headers?.Accept.ToString()?.Contains("application/json") ?? false);

        logger.LogInformation("OnPostSetFecha caso={Id} valorRecibido='{Fecha}'", id, fecha);
        if (string.IsNullOrWhiteSpace(fecha))
        {
            repository.ClearFechaUltimaCarpeta(id);
            if (isAjax)
            {
                return new JsonResult(new { success = true, fecha = "", sector = "—" });
            }
            return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
        }

        if (!DateOnly.TryParseExact(fecha.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            && !SpanishDate.TryParse(fecha, out parsed))
        {
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = "Fecha no válida. Ingrese una fecha válida (ej: dd/mm/aaaa)." }) { StatusCode = 400 };
            }
            Message = "Fecha no válida. Ingrese una fecha válida (ej: dd/mm/aaaa).";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        if (isAjax)
        {
            var sector = FolderSectorRule.For(parsed).ToDisplayName();
            return new JsonResult(new { success = true, fecha = parsed.ToString("yyyy-MM-dd"), sector });
        }
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
    }

    public IActionResult OnPostSetPersonData(long id, string nombre, string rut)
    {
        var isAjax = string.Equals(HttpContext?.Request?.Headers?.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                     || (HttpContext?.Request?.Headers?.Accept.ToString()?.Contains("application/json") ?? false);

        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        // Uppercase to match the casing PersonDataExtractor already uses for auto-extracted names,
        // so manually-corrected cases don't end up in a different case than the rest of the report.
        nombre = (nombre ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedRut is null || nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            var errorMsg = normalizedRut is null
                ? "El RUT ingresado no es válido (revise el dígito verificador)."
                : "Ingrese el nombre completo (al menos nombre y apellido).";

            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }

            Message = errorMsg;
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetPersonData(id, nombre, normalizedRut);
        if (isAjax)
        {
            return new JsonResult(new { success = true, nombre, rut = normalizedRut });
        }
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
    /// <param name="directToCaja">Old physical folders that were never registered: each row is
    /// stored as uploaded and sent straight to the Caja queue in entry order. No email is sent —
    /// those requests were settled with the comuna long ago.</param>
    public IActionResult OnPostAddManualCases(string comuna, List<string> nombre, List<string> rut, bool directToCaja = false)
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
                SourceSubject = directToCaja
                    ? "Carpeta antigua ingresada manualmente (sin correo)"
                    : "Ingresado manualmente por el operador",
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

        var insertedIds = toInsert.Select(repository.Insert).ToList();

        if (directToCaja)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var id in insertedIds)
            {
                repository.MarkUploaded(id, now);
            }
            repository.SendToCaja(insertedIds, now);
            Message = insertedIds.Count == 1
                ? "Carpeta antigua enviada directo a la cola de Caja (sin correo)."
                : $"{insertedIds.Count} carpetas antiguas enviadas directo a la cola de Caja (sin correo).";
            Load();
            return Page();
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
            tombstones.RecordDeletedSourceMessage(sourceMessageId);
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
    /// <summary>"Caja": sends an uploaded/confirmed case, or an F8-reverted (SoloCaja) case, to the
    /// Caja queue without sending any email. The case leaves Casos.</summary>
    public IActionResult OnPostSubirACaja(long id)
    {
        var request = repository.FindById(id);
        if (request is null)
        {
            Message = "El caso no existe.";
            MessageIsError = true;
            Load();
            return Page();
        }

        if (request.SoloCaja && request.FechaUltimaCarpeta is null && !request.SinCarpeta)
        {
            Message = "Debe ingresar la fecha de última carpeta antes de subir la carpeta a Caja.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        Message = $"Carpeta de {request.FullName} enviada a Caja (sin enviar correo).";
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview, search = SearchQuery, bounced = OnlyBounced });
    }

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

        Message = result.Sent
            ? $"{result.Reason} El caso fue trasladado a 'Subidas a Sistema' para asignar Caja o Sin Carpeta."
            : result.Reason;
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
        return DeadlineCalculator.BusinessDaysRemaining(DateOnly.FromDateTime(clock.GetLocalNow().DateTime), deadline);
    }

    private void Load()
    {
        NeedsReviewCount = repository.Count(new CaseQuery { NeedsReview = true });
        BouncedCount = repository.Count(new CaseQuery { Bounced = true });
        DiscardedCount = discardedRepository.Count();
        ComunaOptions = routingService.LoadDirectory()
            .DistinctBy(c => c.Comuna, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Comuna)
            .ToList();

        // Cases transferred out of Casos (to F8, Subidas a Sistema, Caja, or Sin Carpetas)
        // live on their respective dedicated pages instead. Destination, status, review and
        // bounce filters run in SQL; the flags that have no query equivalent are applied below.
        IReadOnlyList<RequestStatus>? statuses = !string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<RequestStatus>(StatusFilter, out var status)
            ? [status]
            : null;
        var all = repository.Find(new CaseQuery
        {
            Destinations = [CaseDestination.None],
            Statuses = statuses,
            NeedsReview = OnlyNeedsReview ? true : null,
            Bounced = OnlyBounced ? true : null
        }).Where(c => c.TransferredAt is null && c.ClosedWithoutFolderAt is null && !c.SinCarpeta);

        // Search by name or RUT (case-insensitive, partial match)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim().ToUpperInvariant();
            all = all.Where(c => MatchesQuery(c, query));

            // Cross-screen matches: transferred cases are hidden from Casos, so the banners
            // tell the operator where the person actually is.
            var f8Matches = repository.Find(new CaseQuery { Destinations = [CaseDestination.F8] }).Where(c => MatchesQuery(c, query)).ToList();
            F8MatchCount = f8Matches.Count;
            F8FirstMatchId = f8Matches.FirstOrDefault()?.Id;

            var subidasMatches = repository.Find(new CaseQuery { Destinations = [CaseDestination.Subidas] }).Where(c => MatchesQuery(c, query)).ToList();
            SubidasMatchCount = subidasMatches.Count;

            var cajaMatches = repository.Find(new CaseQuery { Destinations = [CaseDestination.Caja] }).Where(c => MatchesQuery(c, query)).ToList();
            CajaMatches = cajaMatches.Select(LocateInCaja).ToList();
            CajaMatchCount = CajaMatches.Count;
            CajaMatchBoxCode = CajaMatches.FirstOrDefault()?.BoxCode;
        }

        // Marked cases (checkbox "Marcar") float to the top; confirmed cases (blue row — folder
        // uploaded, comuna already emailed) sink to the very end, ordered by ConfirmedAt ascending
        // so confirmations show in the order they happened. Within each group the list runs from
        // the NEWEST request (top) to the oldest by ReceivedAt (fecha de ingreso — when the email
        // actually arrived), not by CreatedAt (when the row was inserted) and not by the order the
        // operator ticked the checkboxes in (MarkedAt): a case re-tracked later (e.g. after being
        // reverted from Uploaded back to Pending) must keep its original position, and ticking
        // rows in any order must not scramble the queue. The printed sector PDFs still follow
        // MarkedAt (see Sector.cshtml.cs).
        Cases = all
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenByDescending(c => c.Marked && c.SectorPdfGeneratedAt is null)
            .ThenByDescending(c => c.Marked)
            .ThenBy(c => c.FechaUltimaCarpeta is not null)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();
    }

    /// <summary>Position is the 1-based index in the same ordered list the Caja page prints
    /// (GetCasesByBoxId / GetCajaQueue), so the N° shown here matches the paper listing.</summary>
    private CajaLocation LocateInCaja(PersonRequest match)
    {
        if (match.BoxId is { } boxId)
        {
            var box = boxes.FindBoxById(boxId);
            var position = IndexOf(boxes.GetCasesByBoxId(boxId), match.Id);
            return new CajaLocation(match.Id, match.FullName, boxId, box?.Code ?? $"#{boxId}", box?.ClosedAt, position);
        }

        return new CajaLocation(match.Id, match.FullName, null, "cola de Caja", null, IndexOf(repository.GetCajaQueue(), match.Id));
    }

    private static int IndexOf(IReadOnlyList<PersonRequest> cases, long id)
    {
        for (var i = 0; i < cases.Count; i++)
        {
            if (cases[i].Id == id) return i + 1;
        }
        return 0;
    }

    private static bool MatchesQuery(PersonRequest c, string query)
    {
        var queryClean = query.Replace(".", string.Empty).Replace("-", string.Empty);
        var rutClean = (c.Rut ?? string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);
        return (c.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
               rutClean.Contains(queryClean, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Where a searched case physically is inside Caja. BoxId null means the open queue.</summary>
public sealed record CajaLocation(long CaseId, string? FullName, long? BoxId, string BoxCode, DateTimeOffset? ClosedAt, int Position);
