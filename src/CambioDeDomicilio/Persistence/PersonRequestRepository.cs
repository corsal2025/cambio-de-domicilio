using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Persistence;

public interface IPersonRequestRepository
{
    void EnsureSchema();
    bool ExistsBySourceMessageId(string sourceMessageId);
    PersonRequest? FindByRutAndComuna(string rut, string comuna);
    PersonRequest? FindByFullNameAndComuna(string fullName, string comuna);
    PersonRequest? FindPendingBySourceMessageId(string sourceMessageId);
    PersonRequest? FindById(long id);
    long Insert(PersonRequest request);
    void MarkUploaded(long id, DateTimeOffset uploadedAt);
    void SetFechaUltimaCarpeta(long id, DateOnly fecha);
    void ClearFechaUltimaCarpeta(long id);

    /// <summary>Sets "S/C" (Sin Carpeta) in place of a date, clearing FechaUltimaCarpeta.</summary>
    void SetSinCarpeta(long id);
    void SetPersonData(long id, string fullName, string normalizedRut);
    void SetMarked(long id, bool marked);
    void SetFolderNotFound(long id, bool folderNotFound);
    void SetPendienteCarpeta(long id, bool pendienteCarpeta);
    void SetCodigoF8(long id, string? codigoF8);

    /// <summary>Sets the destination screen the case is transferred to, recording when the
    /// transfer happened — used by "Traspaso a F8".</summary>
    void SetDestination(long id, CaseDestination destination, DateTimeOffset transferredAt);

    /// <summary>Undoes a transfer: the case goes back to Casos (Index).</summary>
    void ClearDestination(long id);

    void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long? confirmedByUserId = null);

    /// <summary>Reverts every Uploaded row for this source email back to Pending (clearing UploadedAt) —
    /// used when the original email is found again in the source folder, meaning the operator undid an
    /// accidental upload/move. Confirmed rows are never touched by this: a real confirmation email
    /// already went to the comuna. Returns the number of rows reverted.</summary>
    int RevertUploadedBySourceMessageId(string sourceMessageId);

    /// <summary>Operator-triggered undo of a Confirmed case (paired with sending a rectification
    /// email to the comuna) — resets Status/UploadedAt/ConfirmedAt/ConfirmedByUserId back to a
    /// clean Pending state, as if the case had never been uploaded or confirmed.</summary>
    void RevertConfirmedToPending(long id);

    void SetSectorPdfGenerated(long id, DateTimeOffset generatedAt);

    /// <summary>Permanently removes a case — operator-triggered, for entries that shouldn't have
    /// been tracked at all (e.g. a mistaken manual entry). Not the same as reverting a status.</summary>
    void Delete(long id);

    /// <summary>Tombstones a source email so the poll cycle never re-inserts it as a new case —
    /// without this, deleting a case whose original email is still sitting in "CARP. PARA PEDIR"
    /// gets silently recreated on the very next sync (auto or manual).</summary>
    void RecordDeletedSourceMessage(string sourceMessageId);

    bool IsSourceMessageDeleted(string sourceMessageId);

    IReadOnlyList<PersonRequest> GetAll();
}

public sealed class PersonRequestRepository(string connectionString) : IPersonRequestRepository
{
    public void EnsureSchema()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PersonRequest (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NULL,
                Rut TEXT NULL,
                Comuna TEXT NULL,
                -- Not UNIQUE: one source email can list several contributors, each getting its
                -- own row sharing the same SourceMessageId.
                SourceMessageId TEXT NOT NULL,
                SourceConversationId TEXT NULL,
                SourceSubject TEXT NOT NULL,
                SourceSender TEXT NOT NULL,
                NeedsReview INTEGER NOT NULL,
                Status TEXT NOT NULL,
                ReceivedAt TEXT NOT NULL,
                FechaUltimaCarpeta TEXT NULL,
                UploadedAt TEXT NULL,
                ConfirmedAt TEXT NULL,
                ConfirmedByUserId INTEGER NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);

            CREATE TABLE IF NOT EXISTS DeletedSourceMessage (
                SourceMessageId TEXT PRIMARY KEY,
                DeletedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, "Marked", "Marked INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "SectorPdfGeneratedAt", "SectorPdfGeneratedAt TEXT NULL");
        EnsureColumnExists(connection, "FolderNotFound", "FolderNotFound INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "FolderNotFoundNotifiedAt", "FolderNotFoundNotifiedAt TEXT NULL");
        EnsureColumnExists(connection, "CodigoF8", "CodigoF8 TEXT NULL");
        EnsureColumnExists(connection, "MovedToF8At", "MovedToF8At TEXT NULL");
        EnsureColumnExists(connection, "Destination", "Destination TEXT NOT NULL DEFAULT 'None'");
        EnsureColumnExists(connection, "TransferredAt", "TransferredAt TEXT NULL");
        EnsureColumnExists(connection, "CertificadoNotifiedAt", "CertificadoNotifiedAt TEXT NULL");
        EnsureColumnExists(connection, "PendienteCarpeta", "PendienteCarpeta INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "MarkedAt", "MarkedAt TEXT NULL");
        EnsureColumnExists(connection, "SinCarpeta", "SinCarpeta INTEGER NOT NULL DEFAULT 0");
        RemoveSourceMessageIdUniqueConstraintIfPresent(connection);

        // Backfill migration: cases transferred under the old single-destination mechanism
        // (MovedToF8At) must be recognized under the new generic Destination/TransferredAt
        // mechanism, or they'd silently disappear from the F8 page. MovedToF8At itself is left
        // in place (not dropped) per this project's additive-schema convention. Safe/idempotent:
        // on a fresh database MovedToF8At is always NULL, so the UPDATE affects zero rows.
        using (var backfillCommand = connection.CreateCommand())
        {
            backfillCommand.CommandText = """
                UPDATE PersonRequest SET Destination = 'F8', TransferredAt = MovedToF8At
                WHERE MovedToF8At IS NOT NULL AND Destination = 'None'
                """;
            backfillCommand.ExecuteNonQuery();
        }

        // The Certificado destination was removed. Any case still stored under it is sent back to
        // Casos (Destination reset, TransferredAt cleared) so it doesn't get orphaned off every
        // screen, and so Map's Enum.Parse<CaseDestination> never hits the now-undefined value.
        // The CertificadoNotifiedAt column is left in place per this project's additive-schema
        // convention. Safe/idempotent: affects zero rows on any database that never used it.
        using (var dropCertificadoCommand = connection.CreateCommand())
        {
            dropCertificadoCommand.CommandText =
                "UPDATE PersonRequest SET Destination = 'None', TransferredAt = NULL WHERE Destination = 'Certificado'";
            dropCertificadoCommand.ExecuteNonQuery();
        }
    }

    /// <summary>Additive migration for databases created before multiple contributors per email were
    /// supported, where SourceMessageId was still UNIQUE. SQLite can't drop a column constraint
    /// directly, so this rebuilds the table when the old constraint is detected.</summary>
    private static void RemoveSourceMessageIdUniqueConstraintIfPresent(SqliteConnection connection)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='PersonRequest'";
            var tableSql = checkCommand.ExecuteScalar() as string;
            if (tableSql is null || !tableSql.Contains("SourceMessageId TEXT NOT NULL UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return; // already migrated, or a fresh install that never had the constraint
            }
        }

        using var transaction = connection.BeginTransaction();
        using (var rebuildCommand = connection.CreateCommand())
        {
            rebuildCommand.Transaction = transaction;
            rebuildCommand.CommandText = """
                CREATE TABLE PersonRequest_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    Marked INTEGER NOT NULL DEFAULT 0,
                    SectorPdfGeneratedAt TEXT NULL,
                    FolderNotFound INTEGER NOT NULL DEFAULT 0,
                    FolderNotFoundNotifiedAt TEXT NULL,
                    CodigoF8 TEXT NULL,
                    MovedToF8At TEXT NULL,
                    Destination TEXT NOT NULL DEFAULT 'None',
                    TransferredAt TEXT NULL,
                    CertificadoNotifiedAt TEXT NULL,
                    PendienteCarpeta INTEGER NOT NULL DEFAULT 0,
                    MarkedAt TEXT NULL,
                    SinCarpeta INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO PersonRequest_new
                    (Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                     NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                     FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta)
                SELECT
                    Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                    NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                    FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta
                FROM PersonRequest;
                DROP TABLE PersonRequest;
                ALTER TABLE PersonRequest_new RENAME TO PersonRequest;
                CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);
                """;
            rebuildCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Additive migration for columns added after the table was first created — SQLite has
    /// no "ADD COLUMN IF NOT EXISTS", so check PRAGMA table_info first.</summary>
    private static void EnsureColumnExists(SqliteConnection connection, string columnName, string columnDefinitionSql)
    {
        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info(PersonRequest)";
            using var reader = pragmaCommand.ExecuteReader();
            var nameOrdinal = reader.GetOrdinal("name");
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(nameOrdinal), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE PersonRequest ADD COLUMN {columnDefinitionSql}";
        alterCommand.ExecuteNonQuery();
    }

    public bool ExistsBySourceMessageId(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM PersonRequest WHERE SourceMessageId = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", sourceMessageId);
        return command.ExecuteScalar() is not null;
    }

    /// <summary>Any existing record for this (rut, comuna) — the operator only needs to see a person tracked once.</summary>
    public PersonRequest? FindByRutAndComuna(string rut, string comuna)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE Rut = $rut AND Comuna = $comuna
            ORDER BY Id DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$rut", rut);
        command.Parameters.AddWithValue("$comuna", comuna);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>Any existing record for this (full name, comuna) — used to catch duplicates when
    /// the incoming email has no RUT to key off of (needsReview cases), matched case-insensitively
    /// since PersonDataExtractor preserves the source email's original casing.</summary>
    public PersonRequest? FindByFullNameAndComuna(string fullName, string comuna)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE UPPER(FullName) = UPPER($fullName) AND Comuna = $comuna
            ORDER BY Id DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$fullName", fullName);
        command.Parameters.AddWithValue("$comuna", comuna);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public PersonRequest? FindPendingBySourceMessageId(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE SourceMessageId = $id AND Status = 'Pending'
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$id", sourceMessageId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public PersonRequest? FindById(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PersonRequest WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public long Insert(PersonRequest request)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PersonRequest
                (FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                 NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, CreatedAt)
            VALUES
                ($fullName, $rut, $comuna, $sourceMessageId, $sourceConversationId, $sourceSubject, $sourceSender,
                 $needsReview, $status, $receivedAt, $fechaUltimaCarpeta, $uploadedAt, $confirmedAt, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$fullName", (object?)request.FullName ?? DBNull.Value);
        command.Parameters.AddWithValue("$rut", (object?)request.Rut ?? DBNull.Value);
        command.Parameters.AddWithValue("$comuna", (object?)request.Comuna ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceMessageId", request.SourceMessageId);
        command.Parameters.AddWithValue("$sourceConversationId", (object?)request.SourceConversationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceSubject", request.SourceSubject);
        command.Parameters.AddWithValue("$sourceSender", request.SourceSender);
        command.Parameters.AddWithValue("$needsReview", request.NeedsReview ? 1 : 0);
        command.Parameters.AddWithValue("$status", request.Status.ToString());
        command.Parameters.AddWithValue("$receivedAt", request.ReceivedAt.ToString("O"));
        command.Parameters.AddWithValue("$fechaUltimaCarpeta", (object?)request.FechaUltimaCarpeta?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$uploadedAt", (object?)request.UploadedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmedAt", (object?)request.ConfirmedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", request.CreatedAt.ToString("O"));

        return (long)command.ExecuteScalar()!;
    }

    public void MarkUploaded(long id, DateTimeOffset uploadedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Uploaded', UploadedAt = $uploadedAt
            WHERE Id = $id AND Status = 'Pending'
            """;
        command.Parameters.AddWithValue("$uploadedAt", uploadedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetFechaUltimaCarpeta(long id, DateOnly fecha)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET FechaUltimaCarpeta = $fecha, SinCarpeta = 0 WHERE Id = $id";
        command.Parameters.AddWithValue("$fecha", fecha.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetSinCarpeta(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET FechaUltimaCarpeta = NULL, SinCarpeta = 1 WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void ClearFechaUltimaCarpeta(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET FechaUltimaCarpeta = NULL, SinCarpeta = 0 WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Operator-entered person data for cases the extractor couldn't parse; clears the review flag.</summary>
    public void SetPersonData(long id, string fullName, string normalizedRut)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET FullName = $fullName, Rut = $rut, NeedsReview = 0
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$fullName", fullName);
        command.Parameters.AddWithValue("$rut", normalizedRut);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int RevertUploadedBySourceMessageId(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Pending', UploadedAt = NULL
            WHERE SourceMessageId = $id AND Status = 'Uploaded'
            """;
        command.Parameters.AddWithValue("$id", sourceMessageId);
        return command.ExecuteNonQuery();
    }

    public void RevertConfirmedToPending(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Pending', UploadedAt = NULL, ConfirmedAt = NULL, ConfirmedByUserId = NULL
            WHERE Id = $id AND Status = 'Confirmed'
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetSectorPdfGenerated(long id, DateTimeOffset generatedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET SectorPdfGeneratedAt = $generatedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$generatedAt", generatedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PersonRequest WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void RecordDeletedSourceMessage(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DeletedSourceMessage (SourceMessageId, DeletedAt)
            VALUES ($id, $deletedAt)
            ON CONFLICT (SourceMessageId) DO NOTHING
            """;
        command.Parameters.AddWithValue("$id", sourceMessageId);
        command.Parameters.AddWithValue("$deletedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public bool IsSourceMessageDeleted(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DeletedSourceMessage WHERE SourceMessageId = $id";
        command.Parameters.AddWithValue("$id", sourceMessageId);
        return command.ExecuteScalar() is not null;
    }

    public void SetMarked(long id, bool marked)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // Re-ticking "Marcar" clears SectorPdfGeneratedAt so an already-printed case comes back
        // into the next PDF batch — the operator marks it again specifically to re-print it
        // (e.g. a case that needs to be analyzed separately from the rest). MarkedAt records the
        // order cases were ticked in, so the marked set stays sorted the same way on screen and
        // in the printed PDF.
        command.CommandText = marked
            ? "UPDATE PersonRequest SET Marked = $marked, SectorPdfGeneratedAt = NULL, MarkedAt = $markedAt WHERE Id = $id"
            : "UPDATE PersonRequest SET Marked = $marked, MarkedAt = NULL WHERE Id = $id";
        command.Parameters.AddWithValue("$marked", marked ? 1 : 0);
        if (marked)
        {
            command.Parameters.AddWithValue("$markedAt", DateTimeOffset.UtcNow.ToString("O"));
        }
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetFolderNotFound(long id, bool folderNotFound)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET FolderNotFound = $folderNotFound WHERE Id = $id";
        command.Parameters.AddWithValue("$folderNotFound", folderNotFound ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetPendienteCarpeta(long id, bool pendienteCarpeta)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET PendienteCarpeta = $pendienteCarpeta WHERE Id = $id";
        command.Parameters.AddWithValue("$pendienteCarpeta", pendienteCarpeta ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetCodigoF8(long id, string? codigoF8)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET CodigoF8 = $codigoF8 WHERE Id = $id";
        command.Parameters.AddWithValue("$codigoF8", (object?)codigoF8 ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetDestination(long id, CaseDestination destination, DateTimeOffset transferredAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET Destination = $destination, TransferredAt = $transferredAt WHERE Id = $id";
        command.Parameters.AddWithValue("$destination", destination.ToString());
        command.Parameters.AddWithValue("$transferredAt", transferredAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void ClearDestination(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET Destination = 'None', TransferredAt = NULL WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long? confirmedByUserId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Confirmed', ConfirmedAt = $confirmedAt, ConfirmedByUserId = $confirmedByUserId
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$confirmedAt", confirmedAt.ToString("O"));
        command.Parameters.AddWithValue("$confirmedByUserId", (object?)confirmedByUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<PersonRequest> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PersonRequest ORDER BY Id";
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static PersonRequest Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("Id")),
        FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName")),
        Rut = reader.IsDBNull(reader.GetOrdinal("Rut")) ? null : reader.GetString(reader.GetOrdinal("Rut")),
        Comuna = reader.IsDBNull(reader.GetOrdinal("Comuna")) ? null : reader.GetString(reader.GetOrdinal("Comuna")),
        SourceMessageId = reader.GetString(reader.GetOrdinal("SourceMessageId")),
        SourceConversationId = reader.IsDBNull(reader.GetOrdinal("SourceConversationId")) ? null : reader.GetString(reader.GetOrdinal("SourceConversationId")),
        SourceSubject = reader.GetString(reader.GetOrdinal("SourceSubject")),
        SourceSender = reader.GetString(reader.GetOrdinal("SourceSender")),
        NeedsReview = reader.GetInt32(reader.GetOrdinal("NeedsReview")) == 1,
        Status = Enum.Parse<RequestStatus>(reader.GetString(reader.GetOrdinal("Status"))),
        ReceivedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ReceivedAt"))),
        FechaUltimaCarpeta = reader.IsDBNull(reader.GetOrdinal("FechaUltimaCarpeta")) ? null : DateOnly.Parse(reader.GetString(reader.GetOrdinal("FechaUltimaCarpeta"))),
        UploadedAt = reader.IsDBNull(reader.GetOrdinal("UploadedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UploadedAt"))),
        ConfirmedAt = reader.IsDBNull(reader.GetOrdinal("ConfirmedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ConfirmedAt"))),
        ConfirmedByUserId = reader.IsDBNull(reader.GetOrdinal("ConfirmedByUserId")) ? null : reader.GetInt64(reader.GetOrdinal("ConfirmedByUserId")),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        Marked = reader.GetInt32(reader.GetOrdinal("Marked")) == 1,
        MarkedAt = reader.IsDBNull(reader.GetOrdinal("MarkedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("MarkedAt"))),
        SinCarpeta = reader.GetInt32(reader.GetOrdinal("SinCarpeta")) == 1,
        SectorPdfGeneratedAt = reader.IsDBNull(reader.GetOrdinal("SectorPdfGeneratedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("SectorPdfGeneratedAt"))),
        FolderNotFound = reader.GetInt32(reader.GetOrdinal("FolderNotFound")) == 1,
        PendienteCarpeta = reader.GetInt32(reader.GetOrdinal("PendienteCarpeta")) == 1,
        CodigoF8 = reader.IsDBNull(reader.GetOrdinal("CodigoF8")) ? null : reader.GetString(reader.GetOrdinal("CodigoF8")),
        Destination = Enum.Parse<CaseDestination>(reader.GetString(reader.GetOrdinal("Destination"))),
        TransferredAt = reader.IsDBNull(reader.GetOrdinal("TransferredAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("TransferredAt")))
    };
}
