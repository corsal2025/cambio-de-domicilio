using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Persistence;

public interface IPersonRequestRepository
{
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

    /// <summary>Transfers cases with a physical folder to the open Caja queue: Uploaded/Confirmed
    /// cases, SoloCaja cases, and F8 cases whose folder was found (set to Confirmed). Never moves
    /// SinCarpeta or closed-without-folder cases.</summary>
    void SendToCaja(IReadOnlyList<long> ids, DateTimeOffset transferredAt);

    /// <summary>Permanently removes a case — operator-triggered, for entries that shouldn't have
    /// been tracked at all (e.g. a mistaken manual entry). Not the same as reverting a status.</summary>
    void Delete(long id);

    IReadOnlyList<PersonRequest> GetAll();
}

public sealed class PersonRequestRepository(string connectionString) : IPersonRequestRepository
{
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
        return reader.Read() ? PersonRequestMapper.Map(reader) : null;
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
        return reader.Read() ? PersonRequestMapper.Map(reader) : null;
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
        return reader.Read() ? PersonRequestMapper.Map(reader) : null;
    }

    public PersonRequest? FindById(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PersonRequest WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? PersonRequestMapper.Map(reader) : null;
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
                Destination = CASE WHEN (Destination = 'Caja' AND BoxId IS NULL) OR Destination = 'Subidas' THEN 'None' ELSE Destination END,
                TransferredAt = CASE WHEN (Destination = 'Caja' AND BoxId IS NULL) OR Destination = 'Subidas' THEN NULL ELSE TransferredAt END
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
            results.Add(PersonRequestMapper.Map(reader));
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
            SET ClosedWithoutFolderAt = $closedAt,
                FolderNotFound = 0,
                SinCarpeta = 1,
                Destination = 'SinCarpetas',
                TransferredAt = $closedAt
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
                FolderNotFound = 1,
                SinCarpeta = 0
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
            results.Add(PersonRequestMapper.Map(reader));
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
            results.Add(PersonRequestMapper.Map(reader));
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

    private SqliteConnection Open()
    {
        return SqliteConnectionSetup.OpenConfigured(connectionString);
    }
}
