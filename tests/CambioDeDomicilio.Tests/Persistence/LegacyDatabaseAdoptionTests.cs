using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Persistence.Migrations;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

/// <summary>Production databases predate schema versioning (user_version = 0). The baseline migration
/// must adopt every historical layout without losing or altering a single existing value.</summary>
public class LegacyDatabaseAdoptionTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"adoption-test-{Guid.NewGuid():N}.db");
    private string ConnectionString => $"Data Source={dbPath};Pooling=False";

    private const string OriginalColumns = """
        Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NULL, Rut TEXT NULL, Comuna TEXT NULL,
        SourceMessageId TEXT NOT NULL{0}, SourceConversationId TEXT NULL, SourceSubject TEXT NOT NULL,
        SourceSender TEXT NOT NULL, NeedsReview INTEGER NOT NULL, Status TEXT NOT NULL, ReceivedAt TEXT NOT NULL,
        FechaUltimaCarpeta TEXT NULL, UploadedAt TEXT NULL, ConfirmedAt TEXT NULL, ConfirmedByUserId INTEGER NULL,
        CreatedAt TEXT NOT NULL
        """;

    private static readonly string[] OriginalColumnNames =
    [
        "Id", "FullName", "Rut", "Comuna", "SourceMessageId", "SourceConversationId", "SourceSubject", "SourceSender",
        "NeedsReview", "Status", "ReceivedAt", "FechaUltimaCarpeta", "UploadedAt", "ConfirmedAt", "ConfirmedByUserId", "CreatedAt"
    ];

    [Fact]
    public void OriginalLayoutWithUniqueSourceMessageId_IsAdopted_PreservingEveryRowAndDroppingTheConstraint()
    {
        CreateOriginalLayout(unique: true);
        InsertOriginalCase(1, "msg-1", "Pending", uploadedAt: null, confirmedAt: null);
        InsertOriginalCase(2, "msg-2", "Uploaded", uploadedAt: "2024-01-02T10:00:00+00:00", confirmedAt: null);
        InsertOriginalCase(3, "msg-3", "Confirmed", uploadedAt: "2024-01-02T10:00:00+00:00", confirmedAt: "2024-01-03T10:00:00+00:00");
        Exec("INSERT INTO DeletedSourceMessage VALUES ('gone-1', '2024-01-01T00:00:00+00:00')");
        Exec("INSERT INTO ProcessedBounce VALUES ('bounce-1', '2024-01-01T00:00:00+00:00')");
        Exec("INSERT INTO DiscardedEmail (SourceMessageId, SourceSubject, SourceSender, Reason, DiscardedAt) VALUES ('d-1','s','a@b.cl','r','2024-01-01T00:00:00+00:00')");
        var before = ReadRows("PersonRequest", OriginalColumnNames);

        var result = Migrate();

        Assert.Equal(Migrations.All.Count, result.FinalVersion);
        Assert.Equal(before, ReadRows("PersonRequest", OriginalColumnNames)); // every original value is intact
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM DeletedSourceMessage"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM ProcessedBounce"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM DiscardedEmail"));
        // Two contributors can now share one source email.
        Exec("INSERT INTO PersonRequest (SourceMessageId, SourceSubject, SourceSender, NeedsReview, Status, ReceivedAt, CreatedAt) " +
             "VALUES ('msg-1', 's', 'a@b.cl', 0, 'Pending', '2024-02-01T00:00:00+00:00', '2024-02-01T00:00:00+00:00')");
        Assert.Equal(4L, Scalar("SELECT COUNT(*) FROM PersonRequest"));
    }

    [Fact]
    public void Adoption_MovesUploadedAndConfirmedCasesWithNoDestinationToSubidas_ExactlyOnce()
    {
        CreateOriginalLayout(unique: false);
        InsertOriginalCase(1, "msg-1", "Pending", null, null);
        InsertOriginalCase(2, "msg-2", "Uploaded", "2024-01-02T10:00:00+00:00", null);
        InsertOriginalCase(3, "msg-3", "Confirmed", "2024-01-02T10:00:00+00:00", "2024-01-03T10:00:00+00:00");

        Migrate();

        Assert.Equal("None", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 1"));
        Assert.Equal("Subidas", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 2"));
        Assert.Equal("Subidas", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 3"));
        Assert.Equal("2024-01-03T10:00:00+00:00", Scalar("SELECT TransferredAt FROM PersonRequest WHERE Id = 3"));
    }

    [Fact]
    public void RestartingAnAdoptedDatabase_DoesNotReclassifyAnUploadedCase()
    {
        CreateOriginalLayout(unique: false);
        Migrate();
        // After adoption the operator moves an email to "CARP. YA SUBIDAS": Uploaded, still in Casos.
        InsertOriginalCase(10, "msg-10", "Uploaded", "2024-05-01T10:00:00+00:00", null);
        Exec("UPDATE PersonRequest SET CodigoF8 = 'F8-1' WHERE Id = 10"); // would be re-flagged SoloCaja by the old per-startup UPDATE

        var second = Migrate();

        Assert.Empty(second.AppliedVersions);
        Assert.Equal("None", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 10"));
        Assert.Equal(0L, Scalar("SELECT SoloCaja FROM PersonRequest WHERE Id = 10"));
    }

    [Fact]
    public void IntermediateLayoutWithMovedToF8AtAndCertificado_IsAdoptedWithItsOneTimeBackfills()
    {
        CreateOriginalLayout(unique: false);
        Exec("ALTER TABLE PersonRequest ADD COLUMN MovedToF8At TEXT NULL");
        Exec("ALTER TABLE PersonRequest ADD COLUMN Destination TEXT NOT NULL DEFAULT 'None'");
        Exec("ALTER TABLE PersonRequest ADD COLUMN TransferredAt TEXT NULL");
        Exec("ALTER TABLE PersonRequest ADD COLUMN ClosedWithoutFolderAt TEXT NULL");
        Exec("ALTER TABLE PersonRequest ADD COLUMN CodigoF8 TEXT NULL");
        Exec("ALTER TABLE PersonRequest ADD COLUMN FolderNotFound INTEGER NOT NULL DEFAULT 0");
        InsertOriginalCase(1, "msg-1", "Pending", null, null);
        InsertOriginalCase(2, "msg-2", "Pending", null, null);
        InsertOriginalCase(3, "msg-3", "Pending", null, null);
        InsertOriginalCase(4, "msg-4", "Pending", null, null);
        Exec("UPDATE PersonRequest SET MovedToF8At = '2024-03-01T09:00:00+00:00' WHERE Id = 1");
        Exec("UPDATE PersonRequest SET Destination = 'Certificado', TransferredAt = '2024-03-02T09:00:00+00:00' WHERE Id = 2");
        Exec("UPDATE PersonRequest SET ClosedWithoutFolderAt = '2024-03-03T09:00:00+00:00' WHERE Id = 3");
        Exec("UPDATE PersonRequest SET CodigoF8 = 'F8-9' WHERE Id = 4");

        Migrate();

        Assert.Equal("F8", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 1"));
        Assert.Equal("2024-03-01T09:00:00+00:00", Scalar("SELECT TransferredAt FROM PersonRequest WHERE Id = 1"));
        Assert.Equal("None", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 2"));
        Assert.Null(Scalar("SELECT TransferredAt FROM PersonRequest WHERE Id = 2"));
        Assert.Equal("SinCarpetas", Scalar("SELECT Destination FROM PersonRequest WHERE Id = 3"));
        Assert.Equal(1L, Scalar("SELECT SinCarpeta FROM PersonRequest WHERE Id = 3"));
        Assert.Equal(1L, Scalar("SELECT SoloCaja FROM PersonRequest WHERE Id = 4"));
    }

    [Fact]
    public void FreshDatabase_GetsTheFullCurrentSchema()
    {
        Migrate();

        foreach (var column in new[] { "Marked", "BoxId", "Destination", "SoloCaja", "ClosedWithoutFolderAt", "ConfirmationBouncedAt", "SinCarpeta" })
        {
            Assert.Equal(1L, Scalar($"SELECT COUNT(*) FROM pragma_table_info('PersonRequest') WHERE name = '{column}'"));
        }

        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM pragma_table_info('Box') WHERE name = 'Code'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'DiscardedEmail'"));
    }

    private MigrationResult Migrate() => SchemaMigrator.ForApplication(ConnectionString).Migrate();

    private void CreateOriginalLayout(bool unique)
    {
        Exec($"CREATE TABLE PersonRequest ({string.Format(OriginalColumns, unique ? " UNIQUE" : "")})");
        Exec("CREATE TABLE DeletedSourceMessage (SourceMessageId TEXT PRIMARY KEY, DeletedAt TEXT NOT NULL)");
        Exec("CREATE TABLE ProcessedBounce (BounceMessageId TEXT PRIMARY KEY, ProcessedAt TEXT NOT NULL)");
        Exec("CREATE TABLE Box (Id INTEGER PRIMARY KEY AUTOINCREMENT, Number INTEGER NOT NULL, ClosedAt TEXT NOT NULL)");
        Exec("CREATE TABLE DiscardedEmail (Id INTEGER PRIMARY KEY AUTOINCREMENT, SourceMessageId TEXT NOT NULL UNIQUE, " +
             "SourceSubject TEXT NOT NULL, SourceSender TEXT NOT NULL, Reason TEXT NOT NULL, DiscardedAt TEXT NOT NULL)");
    }

    private void InsertOriginalCase(int id, string messageId, string status, string? uploadedAt, string? confirmedAt)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PersonRequest (Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt)
            VALUES ($id, $name, $rut, 'Catemu', $msg, 'conv', 'Solicitud', 'x@municatemu.cl',
                0, $status, '2024-01-01T08:00:00+00:00', '2023-05-01', $uploaded, $confirmed, NULL, '2024-01-01T08:00:00+00:00')
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", $"PERSONA {id} APELLIDO");
        command.Parameters.AddWithValue("$rut", $"1{id}.785.387-7");
        command.Parameters.AddWithValue("$msg", messageId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$uploaded", (object?)uploadedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmed", (object?)confirmedAt ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private List<string> ReadRows(string table, string[] columns)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", columns)} FROM {table} ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<null>" : Convert.ToString(reader.GetValue(i)))));
        }

        return rows;
    }

    private object? Scalar(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private void Exec(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*"))
        {
            File.Delete(file);
        }
    }
}
