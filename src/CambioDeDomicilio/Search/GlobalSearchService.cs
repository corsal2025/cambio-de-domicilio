using System.Globalization;
using System.Text;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Search;

/// <summary>Every place a record can live. The order is the order the result page shows them in.</summary>
public enum SearchModule
{
    Casos,
    F8,
    Subidas,
    Caja,
    SinCarpetas,
    Descartados,
    Comunas
}

public static class SearchModuleInfo
{
    public static readonly IReadOnlyList<SearchModule> All = Enum.GetValues<SearchModule>();

    public static string DisplayName(SearchModule module) => module switch
    {
        SearchModule.Casos => "Cambio de Domicilio",
        SearchModule.F8 => "F8",
        SearchModule.Subidas => "Subidas a Sistema",
        SearchModule.Caja => "Caja",
        SearchModule.SinCarpetas => "Sin Carpetas",
        SearchModule.Descartados => "Descartados",
        SearchModule.Comunas => "Comunas",
        _ => module.ToString()
    };
}

/// <summary>One match. <see cref="Page"/> and <see cref="Route"/> are the target of the link that opens
/// the record in the module that owns it.</summary>
public sealed record SearchHit(
    SearchModule Module,
    string Title,
    string Detail,
    string Page,
    IReadOnlyDictionary<string, string?> Route);

public sealed record GlobalSearchResult(string Query, IReadOnlyList<SearchHit> Hits)
{
    /// <summary>Always every module: the search never skips one.</summary>
    public IReadOnlyList<SearchModule> SearchedModules => SearchModuleInfo.All;

    public int Count => Hits.Count;
}

/// <summary>
/// One search over every module of the system (Casos, F8, Subidas, Caja, Sin Carpetas, Descartados,
/// Comunas). It replaces the per-screen filters so a person can never "exist" in one module and be
/// invisible to the search box of another.
/// </summary>
public sealed class GlobalSearchService(
    IPersonRequestRepository repository,
    IBoxRepository boxes,
    IDiscardedEmailRepository discarded)
{
    public const int MinQueryLength = 2;

    public GlobalSearchResult Search(string? query, IReadOnlyList<ComunaContact> comunas)
    {
        var raw = (query ?? string.Empty).Trim();
        if (raw.Length < MinQueryLength)
        {
            return new GlobalSearchResult(raw, []);
        }

        var text = Normalize(raw);
        var rutDigits = RutKey(raw);
        var hits = new List<SearchHit>();

        var queue = repository.GetCajaQueue();
        var boxCache = new Dictionary<long, (string Code, IReadOnlyList<PersonRequest> Cases)>();

        foreach (var c in repository.Find(new CaseQuery()).OrderByDescending(c => c.Id))
        {
            if (!CaseMatches(c, text, rutDigits))
            {
                continue;
            }

            hits.Add(ToHit(c, raw, queue, boxCache));
        }

        foreach (var email in discarded.GetAll().OrderByDescending(e => e.Id))
        {
            var haystack = Normalize($"{email.SourceSender} {email.SourceSubject} {email.Reason}");
            if (haystack.Contains(text, StringComparison.Ordinal))
            {
                hits.Add(new SearchHit(
                    SearchModule.Descartados,
                    email.SourceSubject,
                    $"De {email.SourceSender} · {email.Reason}",
                    "/Discarded",
                    new Dictionary<string, string?>()));
            }
        }

        foreach (var contact in comunas)
        {
            var haystack = Normalize($"{contact.Comuna} {contact.ContactEmail} {contact.Domain}");
            if (haystack.Contains(text, StringComparison.Ordinal))
            {
                hits.Add(new SearchHit(
                    SearchModule.Comunas,
                    contact.Comuna,
                    $"{contact.ContactEmail} · {contact.Domain}",
                    "/Comunas",
                    new Dictionary<string, string?>()));
            }
        }

        return new GlobalSearchResult(raw, hits.OrderBy(h => h.Module).ToList());
    }

    private static bool CaseMatches(PersonRequest c, string text, string rutDigits)
    {
        if (Normalize(c.FullName).Contains(text, StringComparison.Ordinal)
            || Normalize(c.Comuna).Contains(text, StringComparison.Ordinal)
            || Normalize(c.CodigoF8).Contains(text, StringComparison.Ordinal))
        {
            return true;
        }

        return rutDigits.Length >= MinQueryLength
            && RutKey(c.Rut).Contains(rutDigits, StringComparison.Ordinal);
    }

    private SearchHit ToHit(
        PersonRequest c,
        string query,
        IReadOnlyList<PersonRequest> queue,
        Dictionary<long, (string Code, IReadOnlyList<PersonRequest> Cases)> boxCache)
    {
        var module = ModuleOf(c);
        var baseDetail = $"RUT {c.Rut ?? "—"} · {c.Comuna ?? "—"} · {StatusLabel(c.Status)}";
        var title = string.IsNullOrWhiteSpace(c.FullName) ? "(sin nombre)" : c.FullName!;
        var id = c.Id.ToString(CultureInfo.InvariantCulture);

        switch (module)
        {
            case SearchModule.Caja when c.BoxId is { } boxId:
                if (!boxCache.TryGetValue(boxId, out var box))
                {
                    box = (boxes.FindBoxById(boxId)?.Code ?? $"#{boxId}", boxes.GetCasesByBoxId(boxId));
                    boxCache[boxId] = box;
                }

                return new SearchHit(module, title,
                    $"{baseDetail} · Caja {box.Code}, N° {PositionOf(box.Cases, c.Id)}",
                    "/Caja",
                    new Dictionary<string, string?> { ["boxId"] = boxId.ToString(CultureInfo.InvariantCulture), ["highlightId"] = id });

            case SearchModule.Caja:
                return new SearchHit(module, title,
                    $"{baseDetail} · en la cola de Caja, N° {PositionOf(queue, c.Id)}",
                    "/Caja",
                    new Dictionary<string, string?> { ["highlightId"] = id });

            case SearchModule.F8:
                return new SearchHit(module, title, baseDetail, "/F8",
                    new Dictionary<string, string?> { ["search"] = query, ["highlightId"] = id });

            case SearchModule.Subidas:
                return new SearchHit(module, title, baseDetail, "/SubidasASistema",
                    new Dictionary<string, string?> { ["search"] = query });

            case SearchModule.SinCarpetas:
                return new SearchHit(module, title, baseDetail, "/SinCarpetas",
                    new Dictionary<string, string?> { ["search"] = query });

            default:
                return new SearchHit(module, title, baseDetail, "/Index",
                    new Dictionary<string, string?> { ["search"] = query });
        }
    }

    /// <summary>The module a case is shown in. Closed-without-folder wins over the destination column,
    /// the same rule the Sin Carpetas page uses.</summary>
    internal static SearchModule ModuleOf(PersonRequest c)
    {
        if (c.ClosedWithoutFolderAt is not null || c.SinCarpeta || c.Destination == CaseDestination.SinCarpetas)
        {
            return SearchModule.SinCarpetas;
        }

        return c.Destination switch
        {
            CaseDestination.F8 => SearchModule.F8,
            CaseDestination.Subidas => SearchModule.Subidas,
            CaseDestination.Caja => SearchModule.Caja,
            _ => SearchModule.Casos
        };
    }

    private static int PositionOf(IReadOnlyList<PersonRequest> cases, long id)
    {
        for (var i = 0; i < cases.Count; i++)
        {
            if (cases[i].Id == id)
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static string StatusLabel(RequestStatus status) => status switch
    {
        RequestStatus.Pending => "Pendiente",
        RequestStatus.Uploaded => "Subido",
        RequestStatus.Confirmed => "Confirmado",
        _ => status.ToString()
    };

    /// <summary>Upper-case and accent-free, so "melendez" finds "MELÉNDEZ".</summary>
    internal static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>RUT reduced to digits and K, so "18.785.387-7", "18785387-7" and "187853877" compare equal.</summary>
    internal static string RutKey(string? value) =>
        new(Normalize(value).Where(ch => char.IsDigit(ch) || ch == 'K').ToArray());
}
