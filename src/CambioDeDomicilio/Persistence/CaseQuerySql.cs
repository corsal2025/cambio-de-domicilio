using System.Text;
using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence;

/// <summary>Turns a <see cref="CaseQuery"/> into a parameterized WHERE clause. Only enum names and
/// fixed column expressions are ever concatenated; every value travels as a parameter.</summary>
internal static class CaseQuerySql
{
    public static string BuildWhere(CaseQuery query, SqliteCommand command)
    {
        var clauses = new List<string>();

        if (query.Destinations is { } destinations)
        {
            clauses.Add(InList("Destination", destinations.Select(d => d.ToString()).ToList(), "dest", command));
        }

        if (query.Statuses is { } statuses)
        {
            clauses.Add(InList("Status", statuses.Select(s => s.ToString()).ToList(), "status", command));
        }

        if (query.NeedsReview is { } needsReview)
        {
            clauses.Add(needsReview ? "NeedsReview = 1" : "NeedsReview = 0");
        }

        if (query.Bounced is { } bounced)
        {
            clauses.Add(bounced ? "ConfirmationBouncedAt IS NOT NULL" : "ConfirmationBouncedAt IS NULL");
        }

        if (query.Marked is { } marked)
        {
            clauses.Add(marked ? "Marked = 1" : "Marked = 0");
        }

        if (query.Transferred is { } transferred)
        {
            clauses.Add(transferred ? "TransferredAt IS NOT NULL" : "TransferredAt IS NULL");
        }

        if (query.InSinCarpetasBucket is { } inBucket)
        {
            const string bucket = "(Destination = 'SinCarpetas' OR ClosedWithoutFolderAt IS NOT NULL OR SinCarpeta = 1)";
            clauses.Add(inBucket ? bucket : $"NOT {bucket}");
        }

        return clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
    }

    private static string InList(string column, IReadOnlyList<string> values, string parameterPrefix, SqliteCommand command)
    {
        if (values.Count == 0)
        {
            return "0 = 1";
        }

        var names = new StringBuilder();
        for (var i = 0; i < values.Count; i++)
        {
            var name = $"${parameterPrefix}{i}";
            command.Parameters.AddWithValue(name, values[i]);
            names.Append(i == 0 ? name : ", " + name);
        }

        return $"{column} IN ({names})";
    }
}
