using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Persistence;

public interface IDiscardedEmailRepository
{
    void EnsureSchema();
    bool ExistsBySourceMessageId(string sourceMessageId);
    void Insert(DiscardedEmail email);
    IReadOnlyList<DiscardedEmail> GetAll();
}

public sealed class DiscardedEmailRepository(string connectionString) : IDiscardedEmailRepository
{
    public void EnsureSchema()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DiscardedEmail (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceMessageId TEXT NOT NULL UNIQUE,
                SourceSubject TEXT NOT NULL,
                SourceSender TEXT NOT NULL,
                Reason TEXT NOT NULL,
                DiscardedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public bool ExistsBySourceMessageId(string sourceMessageId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DiscardedEmail WHERE SourceMessageId = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", sourceMessageId);
        return command.ExecuteScalar() is not null;
    }

    public void Insert(DiscardedEmail email)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DiscardedEmail (SourceMessageId, SourceSubject, SourceSender, Reason, DiscardedAt)
            VALUES ($sourceMessageId, $sourceSubject, $sourceSender, $reason, $discardedAt)
            """;
        command.Parameters.AddWithValue("$sourceMessageId", email.SourceMessageId);
        command.Parameters.AddWithValue("$sourceSubject", email.SourceSubject);
        command.Parameters.AddWithValue("$sourceSender", email.SourceSender);
        command.Parameters.AddWithValue("$reason", email.Reason);
        command.Parameters.AddWithValue("$discardedAt", email.DiscardedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<DiscardedEmail> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DiscardedEmail ORDER BY Id DESC";
        using var reader = command.ExecuteReader();
        var results = new List<DiscardedEmail>();
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

    private static DiscardedEmail Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("Id")),
        SourceMessageId = reader.GetString(reader.GetOrdinal("SourceMessageId")),
        SourceSubject = reader.GetString(reader.GetOrdinal("SourceSubject")),
        SourceSender = reader.GetString(reader.GetOrdinal("SourceSender")),
        Reason = reader.GetString(reader.GetOrdinal("Reason")),
        DiscardedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("DiscardedAt")))
    };
}
