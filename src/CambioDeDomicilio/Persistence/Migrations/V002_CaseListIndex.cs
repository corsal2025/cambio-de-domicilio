using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

/// <summary>Every dashboard page selects cases by destination (and often status), see <see cref="CaseQuery"/>.</summary>
internal sealed class V002_CaseListIndex : IMigration
{
    public int Version => 2;

    public string Description => "Index PersonRequest by (Destination, Status) for dashboard listings";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "CREATE INDEX IF NOT EXISTS IX_PersonRequest_DestinationStatus ON PersonRequest (Destination, Status)";
        command.ExecuteNonQuery();
    }
}
