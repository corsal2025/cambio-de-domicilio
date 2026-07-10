using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Directories;

public interface IComunaDirectory
{
    IReadOnlyList<ComunaContact> LoadFromCsv(string csvPath);

    /// <summary>
    /// Resolves a sender's full email address to a known comuna, or null if not in the directory
    /// or if it equals the organization's own domain. Tries an exact address match first (needed
    /// for shared webmail domains like gmail.com used by more than one comuna); falls back to a
    /// domain-only match only when exactly one comuna is registered for that domain, since guessing
    /// among several would misattribute a case to the wrong comuna.
    /// </summary>
    ComunaContact? ResolveByDomain(string senderEmailAddress, string ownDomain, IReadOnlyList<ComunaContact> contacts);

    /// <summary>
    /// Persists a corrected contact email for a comuna back to the CSV (the same file the
    /// polling cycle reads, so the next confirmation send uses the new address).
    /// Returns false when the comuna is not in the directory or the email is not valid.
    /// </summary>
    bool UpdateContactEmail(string csvPath, string comuna, string newEmail);

    /// <summary>
    /// Appends a brand-new comuna/domain/contact-email row to the CSV, for comunas not yet in the
    /// directory (or an additional domain for an existing one). Returns false when the comuna,
    /// domain or email don't have a valid shape.
    /// </summary>
    bool AddContact(string csvPath, string comuna, string contactEmail, string domain);
}

public sealed class ComunaDirectory : IComunaDirectory
{
    public IReadOnlyList<ComunaContact> LoadFromCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            return [];
        }

        var contacts = new List<ComunaContact>();
        var lines = File.ReadAllLines(csvPath);

        foreach (var line in lines.Skip(1)) // skip header: Comuna,ContactEmail,Domain
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 3)
            {
                continue;
            }

            var comuna = parts[0].Trim();
            var contactEmail = parts[1].Trim();
            var domain = parts[2].Trim().ToLowerInvariant();

            if (comuna.Length == 0 || !contactEmail.Contains('@') || domain.Length == 0)
            {
                continue;
            }

            contacts.Add(new ComunaContact(comuna, contactEmail, domain));
        }

        return contacts
            .GroupBy(c => (Comuna: c.Comuna.ToUpperInvariant(), c.Domain))
            .Select(g => g.Last()) // last row wins on duplicate comuna+domain, matching upsert semantics
            .ToList();
    }

    public ComunaContact? ResolveByDomain(string senderEmailAddress, string ownDomain, IReadOnlyList<ComunaContact> contacts)
    {
        var normalizedSender = senderEmailAddress.Trim().ToLowerInvariant();
        var normalizedOwn = ownDomain.Trim().ToLowerInvariant();
        var senderDomain = ExtractDomain(normalizedSender);

        if (senderDomain == normalizedOwn)
        {
            return null;
        }

        var exactAddressMatch = contacts.FirstOrDefault(c =>
            string.Equals(c.ContactEmail, normalizedSender, StringComparison.OrdinalIgnoreCase));
        if (exactAddressMatch is not null)
        {
            return exactAddressMatch;
        }

        // Domain-only match is only safe when a single comuna owns that domain — for a shared
        // webmail domain (e.g. gmail.com) registered to several comunas, an unrecognized address
        // must be discarded for manual review rather than guessed.
        var domainMatches = contacts
            .Where(c => string.Equals(c.Domain, senderDomain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return domainMatches.Count == 1 ? domainMatches[0] : null;
    }

    private static string ExtractDomain(string emailAddress)
    {
        var at = emailAddress.LastIndexOf('@');
        return at >= 0 ? emailAddress[(at + 1)..] : emailAddress;
    }

    public bool UpdateContactEmail(string csvPath, string comuna, string newEmail)
    {
        newEmail = newEmail.Trim();
        if (!IsValidEmailShape(newEmail))
        {
            return false;
        }

        var contacts = LoadFromCsv(csvPath);
        var target = contacts.FirstOrDefault(c =>
            string.Equals(c.Comuna, comuna, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        var updated = contacts
            .Select(c => c == target ? c with { ContactEmail = newEmail } : c)
            .ToList();

        // Write to a temp file then move, so the polling cycle never reads a half-written directory.
        var tempPath = csvPath + ".tmp";
        var lines = new List<string> { "Comuna,ContactEmail,Domain" };
        lines.AddRange(updated.Select(c => $"{c.Comuna},{c.ContactEmail},{c.Domain}"));
        File.WriteAllLines(tempPath, lines);
        File.Move(tempPath, csvPath, overwrite: true);
        return true;
    }

    private static bool IsValidEmailShape(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 3 && email.IndexOf('@', at + 1) < 0
            && email[(at + 1)..].Contains('.') && !email.Contains(',') && !email.Contains(' ');
    }

    public bool AddContact(string csvPath, string comuna, string contactEmail, string domain)
    {
        comuna = comuna.Trim();
        contactEmail = contactEmail.Trim();
        domain = domain.Trim().ToLowerInvariant();

        if (comuna.Length == 0 || !IsValidEmailShape(contactEmail) || !IsValidDomainShape(domain))
        {
            return false;
        }

        var contacts = LoadFromCsv(csvPath).ToList();
        contacts.Add(new ComunaContact(comuna, contactEmail, domain));

        // Write to a temp file then move, so the polling cycle never reads a half-written directory.
        var tempPath = csvPath + ".tmp";
        var lines = new List<string> { "Comuna,ContactEmail,Domain" };
        lines.AddRange(contacts.Select(c => $"{c.Comuna},{c.ContactEmail},{c.Domain}"));
        File.WriteAllLines(tempPath, lines);
        File.Move(tempPath, csvPath, overwrite: true);
        return true;
    }

    private static bool IsValidDomainShape(string domain) =>
        domain.Length > 0 && !domain.Contains('@') && !domain.Contains(' ')
            && !domain.Contains(',') && domain.Contains('.');
}
