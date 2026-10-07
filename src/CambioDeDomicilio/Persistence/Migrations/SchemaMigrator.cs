using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

internal sealed record MigrationResult(int StartVersion, int FinalVersion, IReadOnlyList<int> AppliedVersions, string? BackupPath);

internal sealed class MigrationFailedException(int version, string description, Exception inner)
    : Exception($"Database migration {version} ('{description}') failed and was rolled back: {inner.Message}", inner)
{
    public int Version { get; } = version;
}

internal sealed class DatabaseNewerThanApplicationException(int databaseVersion, int latestKnownVersion)
    : Exception($"The database is at schema version {databaseVersion}, but this release only knows versions up to {latestKnownVersion}. " +
                "It was written by a newer release; refusing to start so it is not modified.")
{
    public int DatabaseVersion { get; } = databaseVersion;
    public int LatestKnownVersion { get; } = latestKnownVersion;
}

/// <summary>Upgrades the SQLite file to the latest schema version. The version lives in
/// <c>PRAGMA user_version</c> (stored in the database header and rolled back together with the
/// migration's DDL). Each pending migration runs once, in order, in its own immediate transaction;
/// before the first one an online backup of the existing data is written next to the file.</summary>
internal sealed class SchemaMigrator
{
    private const int MaxBackupsPerVersion = 3;

    private readonly string connectionString;
    private readonly IReadOnlyList<IMigration> migrations;
    private readonly TimeProvider clock;

    public SchemaMigrator(string connectionString, IReadOnlyList<IMigration> migrations, TimeProvider? clock = null)
    {
        var ordered = migrations.OrderBy(m => m.Version).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Version != i + 1)
            {
                throw new ArgumentException(
                    $"Migration versions must be unique and contiguous starting at 1; found {ordered[i].Version} at position {i + 1}.",
                    nameof(migrations));
            }
        }

        this.connectionString = connectionString;
        this.migrations = ordered;
        this.clock = clock ?? TimeProvider.System;
    }

    public static SchemaMigrator ForApplication(string connectionString) => new(connectionString, Migrations.All);

    public int LatestVersion => migrations.Count;

    public MigrationResult Migrate()
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        var startVersion = ReadVersion(connection);

        if (startVersion > LatestVersion)
        {
            throw new DatabaseNewerThanApplicationException(startVersion, LatestVersion);
        }

        var pending = migrations.Where(m => m.Version > startVersion).ToList();
        if (pending.Count == 0)
        {
            return new MigrationResult(startVersion, startVersion, [], null);
        }

        var backupPath = BackupIfDatabaseHasData(connection, startVersion);
        SqliteConnectionSetup.EnableWriteAheadLogging(connection);

        var applied = new List<int>();
        foreach (var migration in pending)
        {
            ApplyOne(connection, migration);
            applied.Add(migration.Version);
        }

        return new MigrationResult(startVersion, LatestVersion, applied, backupPath);
    }

    private static void ApplyOne(SqliteConnection connection, IMigration migration)
    {
        // Serializable maps to BEGIN IMMEDIATE: the write lock is taken up front, so no other
        // connection can interleave a write with the migration.
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            migration.Apply(connection, transaction);

            using var setVersion = connection.CreateCommand();
            setVersion.Transaction = transaction;
            setVersion.CommandText = $"PRAGMA user_version = {migration.Version}";
            setVersion.ExecuteNonQuery();

            transaction.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackError)
            {
                // The migration error is the one the operator needs; a broken connection must not hide it.
                // SQLite discards an unfinished transaction when the connection closes anyway.
                System.Diagnostics.Debug.WriteLine($"Rollback of migration {migration.Version} failed: {rollbackError.Message}");
            }

            throw new MigrationFailedException(migration.Version, migration.Description, ex);
        }
    }

    private string? BackupIfDatabaseHasData(SqliteConnection source, int currentVersion)
    {
        using var hasTables = source.CreateCommand();
        hasTables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        if (Convert.ToInt32(hasTables.ExecuteScalar()) == 0)
        {
            return null; // brand-new database: nothing to protect
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:")
        {
            return null;
        }

        var stamp = clock.GetUtcNow().ToString("yyyyMMddHHmmss");
        var backupPath = $"{dataSource}.bak-v{currentVersion}-{stamp}";
        using var destination = new SqliteConnection($"Data Source={backupPath};Pooling=False");
        source.BackupDatabase(destination);
        PruneOldBackups(dataSource, currentVersion);
        return backupPath;
    }

    /// <summary>A failing migration rolls back and the scheduler restarts the process, so every retry would
    /// otherwise copy the whole database (personal data) again. Names embed a sortable UTC stamp, so
    /// ordering them as text orders them by time.</summary>
    private static void PruneOldBackups(string dataSource, int version)
    {
        var fullPath = Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(fullPath)!;
        var prefix = $"{Path.GetFileName(fullPath)}.bak-v{version}-";
        var backups = Directory.GetFiles(directory, prefix + "*")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var stale in backups.Take(Math.Max(0, backups.Count - MaxBackupsPerVersion)))
        {
            File.Delete(stale);
        }
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
