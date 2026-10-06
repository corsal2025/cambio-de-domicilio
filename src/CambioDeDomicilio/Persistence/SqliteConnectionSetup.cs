using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence;

/// <summary>Per-connection SQLite settings shared by every repository. The sync cycle and the
/// dashboard write from different threads, each through its own short-lived connection, so a
/// writer must wait for the lock instead of failing with "database is locked".</summary>
internal static class SqliteConnectionSetup
{
    private const int BusyTimeoutMilliseconds = 5000;

    /// <summary>busy_timeout is per connection and must be applied on every open.</summary>
    public static void Configure(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds}";
        command.ExecuteNonQuery();
    }

    /// <summary>Write-ahead logging is stored in the database file, so it only needs to be switched on once
    /// (from EnsureSchema). Readers then no longer block the single writer and vice versa.</summary>
    public static void EnableWriteAheadLogging(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL";
        command.ExecuteNonQuery();
    }

    public static SqliteConnection OpenConfigured(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        Configure(connection);
        return connection;
    }
}
