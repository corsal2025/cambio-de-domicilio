using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Directories;

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
    /// Persists a corrected domain and/or contact email for a comuna back to the CSV (the same
    /// file the polling cycle reads, so the next confirmation send uses the new address). The
    /// existing row is located by its current comuna+domain pair, since a comuna can have more
    /// than one domain on file. Returns false when no row matches that pair, or the new domain/
    /// email don't have a valid shape.
    /// </summary>
    bool UpdateContact(string csvPath, string comuna, string domain, string newDomain, string newEmail);

    /// <summary>
    /// Appends a brand-new comuna/domain/contact-email row to the CSV, for comunas not yet in the
    /// directory (or an additional domain for an existing one). Returns false when the comuna,
    /// domain or email don't have a valid shape.
    /// </summary>
    bool AddContact(string csvPath, string comuna, string contactEmail, string domain);

    /// <summary>
    /// Removes one comuna/domain row from the CSV — for a comuna that no longer requests folders,
    /// or a contact address that no longer applies. Returns false when no row matches that
    /// comuna+domain pair.
    /// </summary>
    bool DeleteContact(string csvPath, string comuna, string domain);
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

            var parts = ParseCsvLine(line);
            if (parts.Count < 3)
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

    /// <summary>
    /// Minimal RFC-4180-style CSV line parser: splits on commas but respects double-quoted
    /// fields (so a quoted field may contain commas) and the "" escape for a literal quote
    /// inside a quoted field. The directory CSV is an internal, admin-maintained file — this
    /// guards against the rare case of a comuna or contact name containing a comma.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    /// <summary>Quotes a CSV field when it contains a comma, quote, or newline; escapes embedded quotes.</summary>
    private static string QuoteCsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public bool UpdateContact(string csvPath, string comuna, string domain, string newDomain, string newEmail)
    {
        newEmail = newEmail.Trim();
        newDomain = newDomain.Trim().ToLowerInvariant();
        if (!EmailShapeValidator.IsValidEmailShape(newEmail) || !IsValidDomainShape(newDomain))
        {
            return false;
        }

        var contacts = LoadFromCsv(csvPath);
        var target = contacts.FirstOrDefault(c =>
            string.Equals(c.Comuna, comuna, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Domain, domain, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        var updated = contacts
            .Select(c => c == target ? c with { ContactEmail = newEmail, Domain = newDomain } : c)
            .ToList();

        WriteCsv(csvPath, updated);
        return true;
    }

    public bool DeleteContact(string csvPath, string comuna, string domain)
    {
        var contacts = LoadFromCsv(csvPath);
        var target = contacts.FirstOrDefault(c =>
            string.Equals(c.Comuna, comuna, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Domain, domain, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        WriteCsv(csvPath, contacts.Where(c => c != target).ToList());
        return true;
    }

    // Write to a temp file then move, so the polling cycle never reads a half-written directory.
    private static void WriteCsv(string csvPath, IReadOnlyList<ComunaContact> contacts)
    {
        var tempPath = csvPath + ".tmp";
        var lines = new List<string> { "Comuna,ContactEmail,Domain" };
        lines.AddRange(contacts.Select(c => $"{QuoteCsvField(c.Comuna)},{QuoteCsvField(c.ContactEmail)},{QuoteCsvField(c.Domain)}"));
        File.WriteAllLines(tempPath, lines);
        File.Move(tempPath, csvPath, overwrite: true);
    }

    public bool AddContact(string csvPath, string comuna, string contactEmail, string domain)
    {
        comuna = comuna.Trim();
        contactEmail = contactEmail.Trim();
        domain = domain.Trim().ToLowerInvariant();

        if (comuna.Length == 0 || !EmailShapeValidator.IsValidEmailShape(contactEmail) || !IsValidDomainShape(domain))
        {
            return false;
        }

        var contacts = LoadFromCsv(csvPath).ToList();
        contacts.Add(new ComunaContact(comuna, contactEmail, domain));

        WriteCsv(csvPath, contacts);
        return true;
    }

    private static bool IsValidDomainShape(string domain) =>
        domain.Length > 0 && !domain.Contains('@') && !domain.Contains(' ')
            && !domain.Contains(',') && domain.Contains('.');
}
