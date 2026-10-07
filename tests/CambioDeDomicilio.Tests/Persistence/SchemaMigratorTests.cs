using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Persistence.Migrations;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

public class SchemaMigratorTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"migrator-test-{Guid.NewGuid():N}.db");
    private string ConnectionString => $"Data Source={dbPath}";

    [Fact]
    public void Migrate_FreshDatabase_EndsAtLatestVersion_AndSecondRunAppliesNothing()
    {
        var migrations = new IMigration[] { new CreateTable(1, "A"), new CreateTable(2, "B") };
        var sut = new SchemaMigrator(ConnectionString, migrations);

        var first = sut.Migrate();
        var second = sut.Migrate();

        Assert.Equal(2, first.FinalVersion);
        Assert.Equal([1, 2], first.AppliedVersions);
        Assert.Equal(2, ReadUserVersion());
        Assert.Empty(second.AppliedVersions);
        Assert.Equal(2, second.FinalVersion);
    }

    [Fact]
    public void Migrate_PendingMigrations_RunInAscendingOrder_EachOnce()
    {
        var calls = new List<int>();
        var migrations = new IMigration[] { new Recording(3, calls), new Recording(1, calls), new Recording(2, calls) };

        new SchemaMigrator(ConnectionString, migrations).Migrate();
        new SchemaMigrator(ConnectionString, migrations).Migrate();

        Assert.Equal([1, 2, 3], calls);
    }

    [Fact]
    public void Migrate_OnlyRunsMigrationsAboveCurrentVersion()
    {
        var calls = new List<int>();
        new SchemaMigrator(ConnectionString, [new Recording(1, calls)]).Migrate();

        new SchemaMigrator(ConnectionString, [new Recording(1, calls), new Recording(2, calls)]).Migrate();

        Assert.Equal([1, 2], calls);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    public void Constructor_DuplicateOrNonContiguousVersions_Throws(int a, int b)
    {
        var migrations = new IMigration[] { new CreateTable(a, "A"), new CreateTable(b, "B") };

        Assert.Throws<ArgumentException>(() => new SchemaMigrator(ConnectionString, migrations));
    }

    [Fact]
    public void Migrate_FailingMigration_RollsBackItsChanges_KeepsVersion_AndNamesFailingVersion()
    {
        var migrations = new IMigration[] { new CreateTable(1, "Kept"), new CreateThenThrow(2, "Lost") };

        var ex = Assert.Throws<MigrationFailedException>(() => new SchemaMigrator(ConnectionString, migrations).Migrate());

        Assert.Equal(2, ex.Version);
        Assert.Contains("2", ex.Message);
        Assert.Equal(1, ReadUserVersion());
        Assert.True(TableExists("Kept"));
        Assert.False(TableExists("Lost"));
    }

    [Fact]
    public void Migrate_DatabaseNewerThanKnownMigrations_IsRefusedWithoutModification()
    {
        new SchemaMigrator(ConnectionString, [new CreateTable(1, "A"), new CreateTable(2, "B")]).Migrate();
        SqliteConnection.ClearAllPools();
        var hashBefore = Hash();

        var ex = Assert.Throws<DatabaseNewerThanApplicationException>(
            () => new SchemaMigrator(ConnectionString, [new CreateTable(1, "A")]).Migrate());

        SqliteConnection.ClearAllPools();
        Assert.Equal(2, ex.DatabaseVersion);
        Assert.Equal(1, ex.LatestKnownVersion);
        Assert.Equal(hashBefore, Hash());
        Assert.Empty(BackupFiles());
    }

    [Fact]
    public void Migrate_ExistingDatabaseWithPendingMigration_CreatesBackupWithPreUpgradeData()
    {
        new SchemaMigrator(ConnectionString, [new CreateTable(1, "Cases")]).Migrate();
        Exec("INSERT INTO Cases (Id) VALUES (42)");

        var result = new SchemaMigrator(ConnectionString, [new CreateTable(1, "Cases"), new CreateTable(2, "Boxes")]).Migrate();

        var backup = Assert.Single(BackupFiles());
        Assert.Equal(result.BackupPath, backup);
        Assert.Contains("bak-v1-", Path.GetFileName(backup));
        using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Cases";
        Assert.Equal(42L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'Boxes'";
        Assert.Equal(0L, command.ExecuteScalar()); // taken before migration 2 ran
    }

    [Fact]
    public void Migrate_RepeatedFailedAttempts_KeepAtMostThreeBackups_IncludingTheNewest()
    {
        new SchemaMigrator(ConnectionString, [new CreateTable(1, "Cases")]).Migrate();
        Exec("INSERT INTO Cases (Id) VALUES (1)");
        var clock = new AdvancingClock();
        var migrations = new IMigration[] { new CreateTable(1, "Cases"), new CreateThenThrow(2, "Lost") };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Throws<MigrationFailedException>(() => new SchemaMigrator(ConnectionString, migrations, clock).Migrate());
        }

        var backups = BackupFiles().OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.Equal(3, backups.Length);
        // Names embed the clock reading, so the newest attempt (minute 5) must still be there and minutes 1-2 must be gone.
        Assert.EndsWith("0005" + "00", Path.GetFileName(backups[^1])[^6..]);
        Assert.DoesNotContain(backups, f => Path.GetFileName(f).EndsWith("000100", StringComparison.Ordinal));
    }

    [Fact]
    public void Migrate_BackupsOfDifferentVersionsAreNotPruned()
    {
        var clock = new AdvancingClock();
        new SchemaMigrator(ConnectionString, [new CreateTable(1, "A")], clock).Migrate();
        for (var version = 1; version <= 4; version++)
        {
            var list = Enumerable.Range(1, version + 1).Select(v => (IMigration)new CreateTable(v, $"T{v}")).ToArray();
            new SchemaMigrator(ConnectionString, list, clock).Migrate();
        }

        // One backup per upgrade, each at a different version: none may be pruned.
        Assert.Equal(4, BackupFiles().Length);
    }

    [Fact]
    public void Migrate_FailureWhileRollingBack_StillReportsTheOriginalError()
    {
        var ex = Assert.Throws<MigrationFailedException>(
            () => new SchemaMigrator(ConnectionString, [new CloseConnectionThenThrow(1)]).Migrate());

        Assert.Equal(1, ex.Version);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void Migrate_FreshDatabase_DoesNotCreateBackup()
    {
        var result = new SchemaMigrator(ConnectionString, [new CreateTable(1, "A")]).Migrate();

        Assert.Null(result.BackupPath);
        Assert.Empty(BackupFiles());
    }

    [Fact]
    public void Migrate_AlreadyAtLatest_DoesNotCreateBackup()
    {
        var migrations = new IMigration[] { new CreateTable(1, "A") };
        new SchemaMigrator(ConnectionString, migrations).Migrate();

        var result = new SchemaMigrator(ConnectionString, migrations).Migrate();

        Assert.Null(result.BackupPath);
        Assert.Empty(BackupFiles());
    }

    [Fact]
    public void Migrate_LegacyDatabaseAtVersionZeroWithTables_IsBackedUpBeforeAdoption()
    {
        Exec("CREATE TABLE Legacy (Id INTEGER)");
        Exec("INSERT INTO Legacy VALUES (7)");

        var result = new SchemaMigrator(ConnectionString, [new CreateTable(1, "A")]).Migrate();

        Assert.NotNull(result.BackupPath);
        Assert.Contains("bak-v0-", Path.GetFileName(result.BackupPath!));
    }

    [Fact]
    public async Task Migrate_WhileMigrationRuns_OtherConnectionCannotWrite()
    {
        var migrationStarted = new ManualResetEventSlim();
        var allowFinish = new ManualResetEventSlim();
        new SchemaMigrator(ConnectionString, [new CreateTable(1, "Shared")]).Migrate();
        var blocking = new BlockingMigration(2, migrationStarted, allowFinish);
        var migrator = Task.Run(() => new SchemaMigrator(ConnectionString, [new CreateTable(1, "Shared"), blocking]).Migrate());
        Assert.True(migrationStarted.Wait(TimeSpan.FromSeconds(10)));

        var ex = Assert.Throws<SqliteException>(() =>
        {
            using var other = new SqliteConnection($"{ConnectionString};Pooling=False");
            other.Open();
            using var busy = other.CreateCommand();
            busy.CommandText = "PRAGMA busy_timeout = 100";
            busy.ExecuteNonQuery();
            other.DefaultTimeout = 1; // Microsoft.Data.Sqlite waits for the command timeout, not just the PRAGMA
            using var write = other.CreateCommand();
            write.CommandText = "INSERT INTO Shared (Id) VALUES (1)";
            write.ExecuteNonQuery();
        });

        allowFinish.Set();
        await migrator;
        Assert.Equal(5, ex.SqliteErrorCode); // SQLITE_BUSY
    }

    private int ReadUserVersion() => Convert.ToInt32(Scalar("PRAGMA user_version"));

    private bool TableExists(string name) =>
        Convert.ToInt32(Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{name}'")) == 1;

    private object? Scalar(string sql)
    {
        using var connection = new SqliteConnection($"{ConnectionString};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private void Exec(string sql)
    {
        using var connection = new SqliteConnection($"{ConnectionString};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private string Hash() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dbPath)));

    private string[] BackupFiles() =>
        Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + ".bak-v*");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*"))
        {
            File.Delete(file);
        }
    }

    private sealed class CreateTable(int version, string table) : IMigration
    {
        public int Version => version;
        public string Description => $"create {table}";

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"CREATE TABLE {table} (Id INTEGER)";
            command.ExecuteNonQuery();
        }
    }

    private sealed class CreateThenThrow(int version, string table) : IMigration
    {
        public int Version => version;
        public string Description => "fails midway";

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"CREATE TABLE {table} (Id INTEGER)";
            command.ExecuteNonQuery();
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class AdvancingClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>Each reading is one minute later, so every backup gets a distinct, ordered name.</summary>
        public override DateTimeOffset GetUtcNow() => now = now.AddMinutes(1);
    }

    private sealed class CloseConnectionThenThrow(int version) : IMigration
    {
        public int Version => version;
        public string Description => "breaks the connection, then fails";

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            connection.Close(); // the later Rollback() on the transaction can no longer work
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class Recording(int version, List<int> calls) : IMigration
    {
        public int Version => version;
        public string Description => "recording";
        public void Apply(SqliteConnection connection, SqliteTransaction transaction) => calls.Add(version);
    }

    private sealed class BlockingMigration(int version, ManualResetEventSlim started, ManualResetEventSlim allowFinish) : IMigration
    {
        public int Version => version;
        public string Description => "blocks until released";

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE Extra (Id INTEGER)";
            command.ExecuteNonQuery();
            started.Set();
            allowFinish.Wait(TimeSpan.FromSeconds(10));
        }
    }
}
