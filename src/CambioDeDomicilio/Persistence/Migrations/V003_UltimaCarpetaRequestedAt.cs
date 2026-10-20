using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

/// <summary>The Última Carpeta screen records when the last folder of an F8 case was requested.</summary>
internal sealed class V003_UltimaCarpetaRequestedAt : IMigration
{
    public int Version => 3;

    public string Description => "Add PersonRequest.UltimaCarpetaRequestedAt for the Ultima Carpeta screen";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('PersonRequest') WHERE name = 'UltimaCarpetaRequestedAt'";
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                return;
            }
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE PersonRequest ADD COLUMN UltimaCarpetaRequestedAt TEXT NULL";
        command.ExecuteNonQuery();
    }
}
