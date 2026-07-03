using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Persistence;

public interface IPersonRequestRepository
{
    void EnsureSchema();
    bool ExistsBySourceMessageId(string sourceMessageId);
    PersonRequest? FindByRutAndComuna(string rut, string comuna);
    PersonRequest? FindPendingBySourceMessageId(string sourceMessageId);
    PersonRequest? FindById(long id);
    long Insert(PersonRequest request);
    void MarkUploaded(long id, DateTimeOffset uploadedAt);
    void SetFechaUltimaCarpeta(long id, DateOnly fecha);
    void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long confirmedByUserId);
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
                SourceMessageId TEXT NOT NULL UNIQUE,
                SourceConversationId TEXT NULL,
                SourceSubject TEXT NOT NULL,
                SourceSender TEXT NOT NULL,
                NeedsReview INTEGER NOT NULL,
                Status TEXT NOT NULL,
                FechaUltimaCarpeta TEXT NULL,
                UploadedAt TEXT NULL,
                ConfirmedAt TEXT NULL,
                ConfirmedByUserId INTEGER NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);
            """;
        command.ExecuteNonQuery();
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
                 NeedsReview, Status, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, CreatedAt)
            VALUES
                ($fullName, $rut, $comuna, $sourceMessageId, $sourceConversationId, $sourceSubject, $sourceSender,
                 $needsReview, $status, $fechaUltimaCarpeta, $uploadedAt, $confirmedAt, $createdAt);
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
        command.CommandText = "UPDATE PersonRequest SET FechaUltimaCarpeta = $fecha WHERE Id = $id";
        command.Parameters.AddWithValue("$fecha", fecha.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateStatusToConfirmed(long id, DateTimeOffset confirmedAt, long confirmedByUserId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Confirmed', ConfirmedAt = $confirmedAt, ConfirmedByUserId = $confirmedByUserId
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$confirmedAt", confirmedAt.ToString("O"));
        command.Parameters.AddWithValue("$confirmedByUserId", confirmedByUserId);
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
        FechaUltimaCarpeta = reader.IsDBNull(reader.GetOrdinal("FechaUltimaCarpeta")) ? null : DateOnly.Parse(reader.GetString(reader.GetOrdinal("FechaUltimaCarpeta"))),
        UploadedAt = reader.IsDBNull(reader.GetOrdinal("UploadedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UploadedAt"))),
        ConfirmedAt = reader.IsDBNull(reader.GetOrdinal("ConfirmedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ConfirmedAt"))),
        ConfirmedByUserId = reader.IsDBNull(reader.GetOrdinal("ConfirmedByUserId")) ? null : reader.GetInt64(reader.GetOrdinal("ConfirmedByUserId")),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
    };
}
