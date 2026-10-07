using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Persistence.Migrations;

namespace CambioDeDomicilio.Tests;

/// <summary>Creates the schema for a temp database the same way the application does at startup.</summary>
internal static class TestDatabase
{
    public static MigrationResult Migrate(string dbPath) =>
        SchemaMigrator.ForApplication($"Data Source={dbPath}").Migrate();

    /// <summary>Forces the baseline migration to run again on an already-migrated file, for tests that
    /// first rewrite a table into a historical layout and then check the legacy adoption path.</summary>
    public static MigrationResult RerunBaseline(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 0";
            command.ExecuteNonQuery();
        }

        return Migrate(dbPath);
    }

    /// <summary>Deletes the database and any WAL/backup siblings.</summary>
    public static void Cleanup(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*"))
        {
            File.Delete(file);
        }
    }
}
