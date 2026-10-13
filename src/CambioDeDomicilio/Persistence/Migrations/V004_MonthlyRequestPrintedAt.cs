using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

/// <summary>The monthly folder request listing stamps the cases it includes once the operator marks it.</summary>
internal sealed class V004_MonthlyRequestPrintedAt : IMigration
{
    public int Version => 4;

    public string Description => "Add PersonRequest.MonthlyRequestPrintedAt for the monthly folder request";

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('PersonRequest') WHERE name = 'MonthlyRequestPrintedAt'";
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                return;
            }
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE PersonRequest ADD COLUMN MonthlyRequestPrintedAt TEXT NULL";
        command.ExecuteNonQuery();
    }
}
