using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Dashboard.Auth;

public interface IPasswordResetTokenRepository
{
    void EnsureSchema();
    void Insert(PasswordResetToken token);
    PasswordResetToken? FindByToken(string token);
    void MarkUsed(string token);
    void InvalidateAllForUser(long userId);
}

public sealed class PasswordResetTokenRepository(string connectionString) : IPasswordResetTokenRepository
{
    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PasswordResetToken (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Token TEXT NOT NULL UNIQUE,
                ExpiresAt TEXT NOT NULL,
                UsedAt TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PasswordResetToken_Token ON PasswordResetToken (Token);
            """;
        command.ExecuteNonQuery();
    }

    public void Insert(PasswordResetToken token)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PasswordResetToken (UserId, Token, ExpiresAt, UsedAt, CreatedAt)
            VALUES ($userId, $token, $expiresAt, NULL, $createdAt)
            """;
        command.Parameters.AddWithValue("$userId", token.UserId);
        command.Parameters.AddWithValue("$token", token.Token);
        command.Parameters.AddWithValue("$expiresAt", token.ExpiresAt.ToString("O"));
        command.Parameters.AddWithValue("$createdAt", token.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    public PasswordResetToken? FindByToken(string token)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PasswordResetToken WHERE Token = $token LIMIT 1";
        command.Parameters.AddWithValue("$token", token);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public void MarkUsed(string token)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PasswordResetToken SET UsedAt = $usedAt WHERE Token = $token";
        command.Parameters.AddWithValue("$usedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$token", token);
        command.ExecuteNonQuery();
    }

    public void InvalidateAllForUser(long userId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PasswordResetToken SET UsedAt = $usedAt WHERE UserId = $userId AND UsedAt IS NULL";
        command.Parameters.AddWithValue("$usedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$userId", userId);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static PasswordResetToken Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("Id")),
        UserId = reader.GetInt64(reader.GetOrdinal("UserId")),
        Token = reader.GetString(reader.GetOrdinal("Token")),
        ExpiresAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ExpiresAt"))),
        UsedAt = reader.IsDBNull(reader.GetOrdinal("UsedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UsedAt"))),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
    };
}
