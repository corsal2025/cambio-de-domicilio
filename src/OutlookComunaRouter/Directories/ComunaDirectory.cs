using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Directories;

public interface IComunaDirectory
{
    IReadOnlyList<ComunaContact> LoadFromCsv(string csvPath);

    /// <summary>Resolves a sender email domain to a known comuna, or null if not in the directory
    /// or if it equals the organization's own domain.</summary>
    ComunaContact? ResolveByDomain(string senderDomain, string ownDomain, IReadOnlyList<ComunaContact> contacts);
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
}
