using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

public class SqliteConnectionSetupTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"pragma-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void Migrate_EnablesWriteAheadLogging()
    {
        TestDatabase.Migrate(dbPath);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode"));
    }

    [Fact]
    public void ConnectionsOpenedByRepository_WaitForLocksInsteadOfFailingImmediately()
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        SqliteConnectionSetup.Configure(connection);

        Assert.Equal("5000", Scalar(connection, "PRAGMA busy_timeout"));
        Assert.Equal(5, connection.DefaultTimeout); // what Microsoft.Data.Sqlite actually waits on
    }

    [Fact]
    public async Task ConcurrentWritesFromSeveralConnections_DoNotFailWithDatabaseLocked()
    {
        TestDatabase.Migrate(dbPath);
        var repository = new MessageTombstoneRepository($"Data Source={dbPath}");

        var writers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 25; i++)
            {
                repository.RecordDeletedSourceMessage($"msg-{worker}-{i}");
            }
        }));

        await Task.WhenAll(writers); // throws SqliteException "database is locked" without busy_timeout
        Assert.True(repository.IsSourceMessageDeleted("msg-7-24"));
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
