using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Persistence;

public interface IPersonRequestRepository
{
    void EnsureSchema();
    bool ExistsBySourceMessageId(string sourceMessageId);
    PersonRequest? FindActiveByRutAndComuna(string rut, string comuna);
    long Insert(PersonRequest request);
    void UpdateStatusToSent(long id, string requestMessageId, DateTimeOffset sentAt);
    void UpdateStatusToResponded(long id, string responseMessageId, DateTimeOffset receivedAt, string? lastFolderDate);
    PersonRequest? FindSentByConversationId(string conversationId);
    IReadOnlyList<PersonRequest> FindAllSent();
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
                RequestSentAt TEXT NULL,
                RequestMessageId TEXT NULL,
                ResponseReceivedAt TEXT NULL,
                ResponseMessageId TEXT NULL,
                LastFolderDate TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);
            CREATE INDEX IF NOT EXISTS IX_PersonRequest_ConversationId ON PersonRequest (SourceConversationId);
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

    public PersonRequest? FindActiveByRutAndComuna(string rut, string comuna)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE Rut = $rut AND Comuna = $comuna AND Status IN ('Sent', 'Responded')
            ORDER BY Id DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$rut", rut);
        command.Parameters.AddWithValue("$comuna", comuna);
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
                 NeedsReview, Status, RequestSentAt, RequestMessageId, ResponseReceivedAt, ResponseMessageId,
                 LastFolderDate, CreatedAt)
            VALUES
                ($fullName, $rut, $comuna, $sourceMessageId, $sourceConversationId, $sourceSubject, $sourceSender,
                 $needsReview, $status, $requestSentAt, $requestMessageId, $responseReceivedAt, $responseMessageId,
                 $lastFolderDate, $createdAt);
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
        command.Parameters.AddWithValue("$requestSentAt", (object?)request.RequestSentAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestMessageId", (object?)request.RequestMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$responseReceivedAt", (object?)request.ResponseReceivedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$responseMessageId", (object?)request.ResponseMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastFolderDate", (object?)request.LastFolderDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", request.CreatedAt.ToString("O"));

        return (long)command.ExecuteScalar()!;
    }

    public void UpdateStatusToSent(long id, string requestMessageId, DateTimeOffset sentAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Sent', RequestMessageId = $requestMessageId, RequestSentAt = $sentAt
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$requestMessageId", requestMessageId);
        command.Parameters.AddWithValue("$sentAt", sentAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateStatusToResponded(long id, string responseMessageId, DateTimeOffset receivedAt, string? lastFolderDate)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Status = 'Responded', ResponseMessageId = $responseMessageId,
                ResponseReceivedAt = $receivedAt, LastFolderDate = $lastFolderDate
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$responseMessageId", responseMessageId);
        command.Parameters.AddWithValue("$receivedAt", receivedAt.ToString("O"));
        command.Parameters.AddWithValue("$lastFolderDate", (object?)lastFolderDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public PersonRequest? FindSentByConversationId(string conversationId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE SourceConversationId = $conversationId AND Status = 'Sent'
            ORDER BY Id DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$conversationId", conversationId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<PersonRequest> FindAllSent()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PersonRequest WHERE Status = 'Sent'";
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
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
        RequestSentAt = reader.IsDBNull(reader.GetOrdinal("RequestSentAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("RequestSentAt"))),
        RequestMessageId = reader.IsDBNull(reader.GetOrdinal("RequestMessageId")) ? null : reader.GetString(reader.GetOrdinal("RequestMessageId")),
        ResponseReceivedAt = reader.IsDBNull(reader.GetOrdinal("ResponseReceivedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ResponseReceivedAt"))),
        ResponseMessageId = reader.IsDBNull(reader.GetOrdinal("ResponseMessageId")) ? null : reader.GetString(reader.GetOrdinal("ResponseMessageId")),
        LastFolderDate = reader.IsDBNull(reader.GetOrdinal("LastFolderDate")) ? null : reader.GetString(reader.GetOrdinal("LastFolderDate")),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
    };
}
