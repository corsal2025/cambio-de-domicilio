using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

/// <summary>Every <see cref="CaseQuery"/> must return exactly what the old "GetAll() then LINQ" code
/// returned, in the same order. The fixture covers the cross product of the flags the dashboard pages
/// branch on, and each test compares the SQL result to an in-memory oracle.</summary>
public class CaseQueryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"casequery-test-{Guid.NewGuid():N}.db");
    private readonly PersonRequestRepository repository;

    public CaseQueryTests()
    {
        TestDatabase.Migrate(dbPath);
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        SeedCrossProduct();
    }

    [Fact]
    public void Find_NoFilters_ReturnsEveryCaseInIdOrder()
    {
        AssertSameAsOracle(new CaseQuery(), _ => true);
    }

    [Theory]
    [InlineData(CaseDestination.None)]
    [InlineData(CaseDestination.F8)]
    [InlineData(CaseDestination.Caja)]
    [InlineData(CaseDestination.Subidas)]
    [InlineData(CaseDestination.SinCarpetas)]
    public void Find_ByDestination_MatchesOracle(CaseDestination destination)
    {
        AssertSameAsOracle(new CaseQuery { Destinations = [destination] }, c => c.Destination == destination);
    }

    [Fact]
    public void Find_ByMultipleDestinations_MatchesOracle()
    {
        AssertSameAsOracle(
            new CaseQuery { Destinations = [CaseDestination.Subidas, CaseDestination.None] },
            c => c.Destination is CaseDestination.Subidas or CaseDestination.None);
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Uploaded)]
    [InlineData(RequestStatus.Confirmed)]
    public void Find_ByStatus_MatchesOracle(RequestStatus status)
    {
        AssertSameAsOracle(new CaseQuery { Statuses = [status] }, c => c.Status == status);
    }

    [Fact]
    public void Find_DestinationAndStatusCombined_MatchesOracle()
    {
        AssertSameAsOracle(
            new CaseQuery { Destinations = [CaseDestination.None], Statuses = [RequestStatus.Uploaded, RequestStatus.Confirmed] },
            c => c.Destination == CaseDestination.None && c.Status is RequestStatus.Uploaded or RequestStatus.Confirmed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Find_ByNeedsReview_MatchesOracle(bool value)
    {
        AssertSameAsOracle(new CaseQuery { NeedsReview = value }, c => c.NeedsReview == value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Find_ByBounced_MatchesOracle(bool value)
    {
        AssertSameAsOracle(new CaseQuery { Bounced = value }, c => (c.ConfirmationBouncedAt is not null) == value);
    }

    [Fact]
    public void Find_ByMarked_MatchesOracle()
    {
        AssertSameAsOracle(new CaseQuery { Marked = true }, c => c.Marked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Find_ByTransferred_MatchesOracle(bool value)
    {
        AssertSameAsOracle(new CaseQuery { Transferred = value }, c => (c.TransferredAt is not null) == value);
    }

    [Fact]
    public void Find_BySinCarpetasBucket_MatchesThePageDefinition()
    {
        AssertSameAsOracle(
            new CaseQuery { InSinCarpetasBucket = true },
            c => c.Destination == CaseDestination.SinCarpetas || c.ClosedWithoutFolderAt is not null || c.SinCarpeta);
    }

    [Fact]
    public void Find_DestinationAndMarkedAndNotTransferred_MatchesOracle()
    {
        AssertSameAsOracle(
            new CaseQuery { Destinations = [CaseDestination.F8, CaseDestination.None], Marked = true, Transferred = false },
            c => c.Destination is CaseDestination.F8 or CaseDestination.None && c.Marked && c.TransferredAt is null);
    }

    [Fact]
    public void Count_EqualsFindCount_ForEveryFilterKind()
    {
        foreach (var query in new[]
        {
            new CaseQuery(),
            new CaseQuery { NeedsReview = true },
            new CaseQuery { Bounced = true },
            new CaseQuery { Destinations = [CaseDestination.Caja], Statuses = [RequestStatus.Confirmed] },
            new CaseQuery { InSinCarpetasBucket = true },
        })
        {
            Assert.Equal(repository.Find(query).Count, repository.Count(query));
        }
    }

    [Fact]
    public void Find_EmptyDestinationList_ReturnsNothing()
    {
        Assert.Empty(repository.Find(new CaseQuery { Destinations = [] }));
    }

    [Fact]
    public void Find_ByDestination_UsesTheDestinationStatusIndex()
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT * FROM PersonRequest WHERE Destination IN ('F8') AND Status IN ('Pending') ORDER BY Id";

        var plan = string.Join(" | ", ReadDetails(command));

        Assert.Contains("IX_PersonRequest_DestinationStatus", plan);
    }

    private void AssertSameAsOracle(CaseQuery query, Func<PersonRequest, bool> oracle)
    {
        var expected = repository.GetAll().Where(oracle).Select(c => c.Id).ToList();

        var actual = repository.Find(query).Select(c => c.Id).ToList();

        Assert.NotEmpty(expected); // the fixture must exercise every filter, or this comparison proves nothing
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Count, repository.Count(query));
    }

    private static IEnumerable<string> ReadDetails(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(reader.GetOrdinal("detail"));
        }
    }

    /// <summary>5 destinations × 3 statuses × needs-review × sin-carpeta × closed-without-folder × bounced × marked,
    /// with last-folder dates straddling the sector cutoff (2023-06-30 / 2023-07-01 / none).</summary>
    private void SeedCrossProduct()
    {
        var dates = new string?[] { "2023-06-30", "2023-07-01", null };
        var n = 0;
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        foreach (var destination in Enum.GetValues<CaseDestination>())
        foreach (var status in Enum.GetValues<RequestStatus>())
        foreach (var needsReview in new[] { 0, 1 })
        foreach (var sinCarpeta in new[] { 0, 1 })
        foreach (var closed in new[] { 0, 1 })
        foreach (var bounced in new[] { 0, 1 })
        foreach (var marked in new[] { 0, 1 })
        {
            n++;
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PersonRequest (FullName, Rut, Comuna, SourceMessageId, SourceSubject, SourceSender, NeedsReview, Status,
                    ReceivedAt, CreatedAt, Destination, SinCarpeta, ClosedWithoutFolderAt, ConfirmationBouncedAt, Marked, FechaUltimaCarpeta, TransferredAt)
                VALUES ($name, '18.785.387-7', 'Catemu', $msg, 's', 'a@b.cl', $needsReview, $status,
                    '2024-01-01T08:00:00+00:00', '2024-01-01T08:00:00+00:00', $destination, $sinCarpeta, $closed, $bounced, $marked, $fecha, $transferred)
                """;
            command.Parameters.AddWithValue("$name", $"PERSONA {n}");
            command.Parameters.AddWithValue("$msg", $"msg-{n % 7}"); // several cases share one source email, as in production
            command.Parameters.AddWithValue("$needsReview", needsReview);
            command.Parameters.AddWithValue("$status", status.ToString());
            command.Parameters.AddWithValue("$destination", destination.ToString());
            command.Parameters.AddWithValue("$sinCarpeta", sinCarpeta);
            command.Parameters.AddWithValue("$closed", closed == 1 ? "2024-02-01T00:00:00+00:00" : DBNull.Value);
            command.Parameters.AddWithValue("$bounced", bounced == 1 ? "2024-02-02T00:00:00+00:00" : DBNull.Value);
            command.Parameters.AddWithValue("$marked", marked);
            command.Parameters.AddWithValue("$fecha", (object?)dates[n % dates.Length] ?? DBNull.Value);
            // Mostly TransferredAt follows Destination, but not always (legacy rows): every 5th row breaks the pattern.
            var transferred = (destination == CaseDestination.None) == (n % 5 != 0);
            command.Parameters.AddWithValue("$transferred", transferred ? DBNull.Value : "2024-02-03T00:00:00+00:00");
            command.ExecuteNonQuery();
        }
    }

    public void Dispose() => TestDatabase.Cleanup(dbPath);
}
