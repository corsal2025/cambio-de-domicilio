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

    /// <summary>Unified F8 revert: whether or not the F8 was already uploaded, clears the F8 status
    /// (FolderNotFound, Destination, TransferredAt, UploadedAt, ConfirmedAt, bounce, marks) and returns
    /// the case to Casos as Pending with SoloCaja set, so its only action is "Caja". Data the operator
    /// already typed (FullName, Rut, CodigoF8, FechaUltimaCarpeta, SinCarpeta) is kept, so undoing a
    /// wrong decision never forces retyping. No email is sent.</summary>
    void RevertF8AndReturnToCasos(long id);

    /// <summary>"Sin carpeta" on an F8 case: records ClosedWithoutFolderAt, clears the F8 flag and
    /// returns the case to Casos (Destination None, TransferredAt cleared). No email is sent.</summary>
    void CloseWithoutFolder(long id, DateTimeOffset closedAt);

    /// <summary>Reverts a case closed without folder back to F8 (Destination F8, ClosedWithoutFolderAt cleared).</summary>
    void ReopenSinCarpetaToF8(long id, DateTimeOffset transferredAt);

    void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long? confirmedByUserId = null);

    /// <summary>Every Confirmed case with this RUT — usually one, but the same person can have a
    /// confirmed case for more than one comuna. Used to attach an incoming bounce to its case.</summary>
    IReadOnlyList<PersonRequest> FindConfirmedByRut(string rut);

    /// <summary>Flags that the confirmation email for this case bounced (non-delivery report found
    /// in the inbox). <see cref="ClearConfirmationBounced"/> is the operator's "Marcar resuelto".</summary>
    void SetConfirmationBounced(long id, DateTimeOffset bouncedAt);
    void ClearConfirmationBounced(long id);

    /// <summary>Tombstones a bounce message so a later poll cycle never re-processes the same NDR
    /// (it stays in the inbox). Mirrors <see cref="RecordDeletedSourceMessage"/>.</summary>
    void RecordProcessedBounce(string bounceMessageId);
    bool IsBounceProcessed(string bounceMessageId);

    /// <summary>Reverts every Uploaded row for this source email back to Pending (clearing UploadedAt) —
    /// used when the original email is found again in the source folder, meaning the operator undid an
    /// accidental upload/move. Confirmed rows are never touched by this: a real confirmation email
    /// already went to the comuna. Returns the number of rows reverted.</summary>
    int RevertUploadedBySourceMessageId(string sourceMessageId);

    /// <summary>Operator-triggered undo of a Confirmed case (paired with sending a rectification
    /// email to the comuna) — resets Status/UploadedAt/ConfirmedAt/ConfirmedByUserId/
    /// ConfirmationBouncedAt back to a clean Pending state, as if the case had never been
    /// uploaded or confirmed. Clearing ConfirmationBouncedAt matters here: the confirmation
    /// being retracted is the one that may have bounced, so a stale "REBOTÓ" flag must not
    /// survive onto the next upload/confirm cycle for this case.</summary>
    void RevertConfirmedToPending(long id);

    void SetSectorPdfGenerated(long id, DateTimeOffset generatedAt);

    /// <summary>Cases waiting to be packed — Destination == Caja and not yet assigned to a closed
    /// box — in the order they were sent to Caja (TransferredAt, then Id), which is the physical
    /// order the operator stacks the folders in.</summary>
    IReadOnlyList<PersonRequest> GetCajaQueue();

    /// <summary>Closes the current Caja queue: assigns every currently-queued case to a new,
    /// sequentially-numbered box with a manual or default code (e.g. A1-CD) and returns it. A queue
    /// that reads empty at the moment this runs still gets a box record (so the operator's "Cerrar Caja"
    /// click always has a result to look at), just with zero cases in it.</summary>
    Box CloseBox(string code, DateTimeOffset closedAt);

    /// <summary>Transfers cases with a physical folder to the open Caja queue: Uploaded/Confirmed
    /// cases, SoloCaja cases, and F8 cases whose folder was found (set to Confirmed). Never moves
    /// SinCarpeta or closed-without-folder cases.</summary>
    void SendToCaja(IReadOnlyList<long> ids, DateTimeOffset transferredAt);

    /// <summary>Reopens a closed box: unpacks all its cases back into the open Caja queue and removes the closed box record.</summary>
    void ReopenBox(long boxId);

    /// <summary>Removes an individual case from a closed box and returns it back to Casos (Destination = None).</summary>
    void RemoveCaseFromClosedBox(long personRequestId);

    /// <summary>Every closed box, most recently closed first.</summary>
    IReadOnlyList<Box> GetBoxes();

    Box? FindBoxById(long id);

    /// <summary>Cases packed into a given closed box, in the same insertion order they had in the
    /// queue (TransferredAt, then Id) — this is the order the printed box listing uses.</summary>
    IReadOnlyList<PersonRequest> GetCasesByBoxId(long boxId);

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

            CREATE TABLE IF NOT EXISTS ProcessedBounce (
                BounceMessageId TEXT PRIMARY KEY,
                ProcessedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Box (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Number INTEGER NOT NULL,
                Code TEXT NOT NULL DEFAULT '',
                ClosedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, "Marked", "Marked INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "BoxId", "BoxId INTEGER NULL");
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
        EnsureColumnExists(connection, "ConfirmationBouncedAt", "ConfirmationBouncedAt TEXT NULL");
        EnsureColumnExists(connection, "SoloCaja", "SoloCaja INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "ClosedWithoutFolderAt", "ClosedWithoutFolderAt TEXT NULL");

        using (var updateLegacy = connection.CreateCommand())
        {
            updateLegacy.CommandText = "UPDATE PersonRequest SET SoloCaja = 1 WHERE CodigoF8 IS NOT NULL AND Destination = 'None' AND FolderNotFound = 0;";
            updateLegacy.ExecuteNonQuery();
        }
        EnsureColumnExists(connection, "Code", "Code TEXT NOT NULL DEFAULT ''", "Box");
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
                    SinCarpeta INTEGER NOT NULL DEFAULT 0,
                    ConfirmationBouncedAt TEXT NULL,
                    BoxId INTEGER NULL
                );
                INSERT INTO PersonRequest_new
                    (Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                     NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                     FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta, ConfirmationBouncedAt, BoxId)
                SELECT
                    Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                    NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                    FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta, ConfirmationBouncedAt, BoxId
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
    private static void EnsureColumnExists(SqliteConnection connection, string columnName, string columnDefinitionSql, string tableName = "PersonRequest")
    {
        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = $"PRAGMA table_info({tableName})";
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
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnDefinitionSql}";
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
        // Only pulls the case back out of the Caja queue if it's still unboxed (BoxId IS NULL) —
        // once a box is closed its membership is historical record and must not silently change
        // just because the source email reappeared.
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Pending', UploadedAt = NULL,
                Destination = CASE WHEN Destination = 'Caja' AND BoxId IS NULL THEN 'None' ELSE Destination END,
                TransferredAt = CASE WHEN Destination = 'Caja' AND BoxId IS NULL THEN NULL ELSE TransferredAt END
            WHERE SourceMessageId = $id AND Status = 'Uploaded'
            """;
        command.Parameters.AddWithValue("$id", sourceMessageId);
        return command.ExecuteNonQuery();
    }

    public void RevertConfirmedToPending(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // Same unboxed-only guard as RevertUploadedBySourceMessageId — see its comment.
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Pending', UploadedAt = NULL, ConfirmedAt = NULL, ConfirmedByUserId = NULL,
                ConfirmationBouncedAt = NULL,
                Destination = CASE WHEN Destination = 'Caja' AND BoxId IS NULL THEN 'None' ELSE Destination END,
                TransferredAt = CASE WHEN Destination = 'Caja' AND BoxId IS NULL THEN NULL ELSE TransferredAt END
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

    public IReadOnlyList<PersonRequest> FindConfirmedByRut(string rut)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PersonRequest WHERE Rut = $rut AND Status = 'Confirmed' ORDER BY Id";
        command.Parameters.AddWithValue("$rut", rut);
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public void SetConfirmationBounced(long id, DateTimeOffset bouncedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET ConfirmationBouncedAt = $bouncedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$bouncedAt", bouncedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void ClearConfirmationBounced(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PersonRequest SET ConfirmationBouncedAt = NULL WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void RecordProcessedBounce(string bounceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ProcessedBounce (BounceMessageId, ProcessedAt)
            VALUES ($id, $processedAt)
            ON CONFLICT (BounceMessageId) DO NOTHING
            """;
        command.Parameters.AddWithValue("$id", bounceMessageId);
        command.Parameters.AddWithValue("$processedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public bool IsBounceProcessed(string bounceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM ProcessedBounce WHERE BounceMessageId = $id";
        command.Parameters.AddWithValue("$id", bounceMessageId);
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
            ? "UPDATE PersonRequest SET Marked = 1, SectorPdfGeneratedAt = NULL, MarkedAt = $markedAt, FolderNotFound = 0, PendienteCarpeta = 0 WHERE Id = $id"
            : "UPDATE PersonRequest SET Marked = 0, MarkedAt = NULL WHERE Id = $id";
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
        command.CommandText = folderNotFound
            ? "UPDATE PersonRequest SET FolderNotFound = 1, Marked = 0, MarkedAt = NULL, PendienteCarpeta = 0 WHERE Id = $id"
            : "UPDATE PersonRequest SET FolderNotFound = 0 WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetPendienteCarpeta(long id, bool pendienteCarpeta)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = pendienteCarpeta
            ? "UPDATE PersonRequest SET PendienteCarpeta = 1, Marked = 0, MarkedAt = NULL, FolderNotFound = 0 WHERE Id = $id"
            : "UPDATE PersonRequest SET PendienteCarpeta = 0 WHERE Id = $id";
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
        command.CommandText = "UPDATE PersonRequest SET Destination = $destination, TransferredAt = $transferredAt, SoloCaja = CASE WHEN $destination = 'F8' THEN 0 ELSE SoloCaja END WHERE Id = $id";
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

    public void RevertF8AndReturnToCasos(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Destination = 'None',
                Status = 'Pending',
                SoloCaja = 1,
                FolderNotFound = 0,
                UploadedAt = NULL,
                TransferredAt = NULL,
                ConfirmedAt = NULL,
                ConfirmationBouncedAt = NULL,
                Marked = 0,
                MarkedAt = NULL,
                PendienteCarpeta = 0
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void CloseWithoutFolder(long id, DateTimeOffset closedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET ClosedWithoutFolderAt = $closedAt, FolderNotFound = 0, Destination = 'None', TransferredAt = NULL
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$closedAt", closedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void ReopenSinCarpetaToF8(long id, DateTimeOffset transferredAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET ClosedWithoutFolderAt = NULL,
                Destination = 'F8',
                TransferredAt = $transferredAt,
                FolderNotFound = 1
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$transferredAt", transferredAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long? confirmedByUserId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Confirmed', ConfirmedAt = $confirmedAt, ConfirmedByUserId = $confirmedByUserId,
                Marked = 0, MarkedAt = NULL, PendienteCarpeta = 0
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

    public IReadOnlyList<PersonRequest> GetCajaQueue()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // Cases with physical folders (SinCarpeta == 0) waiting to be packed into a box, in the
        // order the operator sent them to Caja (TransferredAt is ISO-8601 UTC, so text order is time order).
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE Destination = 'Caja' AND BoxId IS NULL AND SinCarpeta = 0
            ORDER BY TransferredAt, Id
            """;
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public void SendToCaja(IReadOnlyList<long> ids, DateTimeOffset transferredAt)
    {
        if (ids.Count == 0) return;
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        foreach (var id in ids)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE PersonRequest
                SET Destination = 'Caja',
                    Status = CASE WHEN SoloCaja = 1 OR Destination = 'F8' THEN 'Confirmed' ELSE Status END,
                    SoloCaja = 0,
                    TransferredAt = $transferredAt,
                    Marked = 0
                WHERE Id = $id
                  AND (Status = 'Uploaded' OR Status = 'Confirmed' OR SoloCaja = 1 OR Destination = 'F8')
                  AND SinCarpeta = 0
                  AND ClosedWithoutFolderAt IS NULL;
                """;
            command.Parameters.AddWithValue("$transferredAt", transferredAt.ToString("O"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public Box CloseBox(string code, DateTimeOffset closedAt)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        long boxId;
        int number;
        var manualCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO Box (Number, Code, ClosedAt)
                VALUES ((SELECT COALESCE(MAX(Number), 0) + 1 FROM Box), $code, $closedAt);
                SELECT last_insert_rowid();
                """;
            insertCommand.Parameters.AddWithValue("$code", (object?)manualCode ?? string.Empty);
            insertCommand.Parameters.AddWithValue("$closedAt", closedAt.ToString("O"));
            boxId = (long)insertCommand.ExecuteScalar()!;
        }

        using (var numberCommand = connection.CreateCommand())
        {
            numberCommand.Transaction = transaction;
            numberCommand.CommandText = "SELECT Number, Code FROM Box WHERE Id = $id";
            numberCommand.Parameters.AddWithValue("$id", boxId);
            using var r = numberCommand.ExecuteReader();
            r.Read();
            number = r.GetInt32(0);
            var storedCode = r.GetString(1);
            if (string.IsNullOrWhiteSpace(storedCode))
            {
                manualCode = $"A{number}-CD";
                using var updateCodeCmd = connection.CreateCommand();
                updateCodeCmd.Transaction = transaction;
                updateCodeCmd.CommandText = "UPDATE Box SET Code = $code WHERE Id = $id";
                updateCodeCmd.Parameters.AddWithValue("$code", manualCode);
                updateCodeCmd.Parameters.AddWithValue("$id", boxId);
                updateCodeCmd.ExecuteNonQuery();
            }
            else
            {
                manualCode = storedCode;
            }
        }

        using (var assignCommand = connection.CreateCommand())
        {
            assignCommand.Transaction = transaction;
            assignCommand.CommandText = "UPDATE PersonRequest SET BoxId = $boxId WHERE Destination = 'Caja' AND BoxId IS NULL AND SinCarpeta = 0";
            assignCommand.Parameters.AddWithValue("$boxId", boxId);
            assignCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return new Box { Id = boxId, Number = number, Code = manualCode, ClosedAt = closedAt };
    }

    public void ReopenBox(long boxId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        // 1. Unpack all cases from this box back to the open queue (BoxId = NULL, Destination remains 'Caja')
        using (var unpackCommand = connection.CreateCommand())
        {
            unpackCommand.Transaction = transaction;
            unpackCommand.CommandText = "UPDATE PersonRequest SET BoxId = NULL WHERE BoxId = $boxId";
            unpackCommand.Parameters.AddWithValue("$boxId", boxId);
            unpackCommand.ExecuteNonQuery();
        }

        // 2. Delete the closed box record
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM Box WHERE Id = $boxId";
            deleteCommand.Parameters.AddWithValue("$boxId", boxId);
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void RemoveCaseFromClosedBox(long personRequestId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Destination = 'None', BoxId = NULL, TransferredAt = NULL
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$id", personRequestId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Box> GetBoxes()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Box ORDER BY Number DESC";
        using var reader = command.ExecuteReader();
        var results = new List<Box>();
        while (reader.Read())
        {
            results.Add(MapBox(reader));
        }
        return results;
    }

    public Box? FindBoxById(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Box WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapBox(reader) : null;
    }

    public IReadOnlyList<PersonRequest> GetCasesByBoxId(long boxId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE BoxId = $boxId
            ORDER BY TransferredAt, Id
            """;
        command.Parameters.AddWithValue("$boxId", boxId);
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    private static Box MapBox(SqliteDataReader reader)
    {
        var codeOrdinal = reader.GetOrdinal("Code");
        var number = reader.GetInt32(reader.GetOrdinal("Number"));
        var code = !reader.IsDBNull(codeOrdinal) && !string.IsNullOrWhiteSpace(reader.GetString(codeOrdinal))
            ? reader.GetString(codeOrdinal)
            : $"A{number}-CD";

        return new Box
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Number = number,
            Code = code,
            ClosedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ClosedAt")))
        };
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

        private static PersonRequest Map(SqliteDataReader reader)
    {
        long? GetNullableInt64(string name)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                return reader.IsDBNull(ord) ? null : reader.GetInt64(ord);
            }
            catch
            {
                return null;
            }
        }

        DateTimeOffset? GetNullableDateTimeOffset(string name)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                return reader.IsDBNull(ord) ? null : DateTimeOffset.Parse(reader.GetString(ord));
            }
            catch
            {
                return null;
            }
        }

        bool GetBoolean(string name)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                return !reader.IsDBNull(ord) && reader.GetInt32(ord) == 1;
            }
            catch
            {
                return false;
            }
        }

        return new PersonRequest
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
            TransferredAt = reader.IsDBNull(reader.GetOrdinal("TransferredAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("TransferredAt"))),
            ConfirmationBouncedAt = reader.IsDBNull(reader.GetOrdinal("ConfirmationBouncedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ConfirmationBouncedAt"))),
            BoxId = GetNullableInt64("BoxId"),
            SoloCaja = GetBoolean("SoloCaja"),
            ClosedWithoutFolderAt = GetNullableDateTimeOffset("ClosedWithoutFolderAt")
        };
    }
}