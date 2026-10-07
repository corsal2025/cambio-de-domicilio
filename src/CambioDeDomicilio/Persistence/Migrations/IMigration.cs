using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

/// <summary>One numbered schema (or one-time data) change. A shipped migration is never edited:
/// production databases have already recorded its version, so a later fix is a new migration.</summary>
internal interface IMigration
{
    /// <summary>1-based, contiguous across <see cref="Migrations.All"/>.</summary>
    int Version { get; }

    string Description { get; }

    /// <summary>Runs inside <paramref name="transaction"/>; every command must be assigned to it.</summary>
    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}
