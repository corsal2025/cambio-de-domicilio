using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence;

/// <summary>Records of emails the poll cycle must never act on again. Kept apart from the cases
/// because they are a write-once log keyed by message id, not part of any case's lifecycle.</summary>
public interface IMessageTombstoneRepository
{
    /// <summary>Tombstones a source email so the poll cycle never re-inserts it as a new case —
    /// without this, deleting a case whose original email is still sitting in "CARP. PARA PEDIR"
    /// gets silently recreated on the very next sync (auto or manual).</summary>
    void RecordDeletedSourceMessage(string sourceMessageId);

    bool IsSourceMessageDeleted(string sourceMessageId);

    /// <summary>Tombstones a bounce message so a later poll cycle never re-processes the same NDR
    /// (it stays in the inbox). Mirrors <see cref="RecordDeletedSourceMessage"/>.</summary>
    void RecordProcessedBounce(string bounceMessageId);

    bool IsBounceProcessed(string bounceMessageId);
}

public sealed class MessageTombstoneRepository(string connectionString) : IMessageTombstoneRepository
{
    public void RecordDeletedSourceMessage(string sourceMessageId) =>
        Record("""
            INSERT INTO DeletedSourceMessage (SourceMessageId, DeletedAt)
            VALUES ($id, $at)
            ON CONFLICT (SourceMessageId) DO NOTHING
            """, sourceMessageId);

    public bool IsSourceMessageDeleted(string sourceMessageId) =>
        Exists("SELECT 1 FROM DeletedSourceMessage WHERE SourceMessageId = $id", sourceMessageId);

    public void RecordProcessedBounce(string bounceMessageId) =>
        Record("""
            INSERT INTO ProcessedBounce (BounceMessageId, ProcessedAt)
            VALUES ($id, $at)
            ON CONFLICT (BounceMessageId) DO NOTHING
            """, bounceMessageId);

    public bool IsBounceProcessed(string bounceMessageId) =>
        Exists("SELECT 1 FROM ProcessedBounce WHERE BounceMessageId = $id", bounceMessageId);

    private void Record(string insertSql, string id)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = insertSql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private bool Exists(string selectSql, string id)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = selectSql;
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is not null;
    }
}
