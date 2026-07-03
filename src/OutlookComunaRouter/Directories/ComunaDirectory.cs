using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Directories;

public interface IComunaDirectory
{
    IReadOnlyList<ComunaContact> LoadFromCsv(string csvPath);

    /// <summary>Resolves a sender email domain to a known comuna, or null if not in the directory
    /// or if it equals the organization's own domain.</summary>
    ComunaContact? ResolveByDomain(string senderDomain, string ownDomain, IReadOnlyList<ComunaContact> contacts);

    /// <summary>
    /// Persists a corrected contact email for a comuna back to the CSV (the same file the
    /// polling cycle reads, so the next confirmation send uses the new address).
    /// Returns false when the comuna is not in the directory or the email is not valid.
    /// </summary>
    bool UpdateContactEmail(string csvPath, string comuna, string newEmail);
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
            .GroupBy(c => c.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last()) // last row wins on duplicate domain, matching upsert semantics
            .ToList();
    }

    public ComunaContact? ResolveByDomain(string senderDomain, string ownDomain, IReadOnlyList<ComunaContact> contacts)
    {
        var normalizedSender = senderDomain.Trim().ToLowerInvariant();
        var normalizedOwn = ownDomain.Trim().ToLowerInvariant();

        if (normalizedSender == normalizedOwn)
        {
            return null;
        }

        return contacts.FirstOrDefault(c =>
            string.Equals(c.Domain, normalizedSender, StringComparison.OrdinalIgnoreCase));
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
}
