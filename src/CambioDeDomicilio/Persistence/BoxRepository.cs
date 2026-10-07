using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Persistence;

/// <summary>Closed boxes of Caja folders: the box records and the operations whose subject is a box.
/// Queue reads/transitions of individual cases (GetCajaQueue, SendToCaja) stay with the case repository.</summary>
public interface IBoxRepository
{
    /// <summary>Closes the current Caja queue: assigns every currently-queued case to a new,
    /// sequentially-numbered box with a manual or default code (e.g. A1-CD) and returns it. A queue
    /// that reads empty at the moment this runs still gets a box record (so the operator's "Cerrar Caja"
    /// click always has a result to look at), just with zero cases in it.</summary>
    Box CloseBox(string code, DateTimeOffset closedAt);

    /// <summary>Reopens a closed box: unpacks all its cases back into the open Caja queue and removes the closed box record.</summary>
    void ReopenBox(long boxId);

    /// <summary>Removes an individual case from a closed box and returns it back to Casos (Destination = None).</summary>
    void RemoveCaseFromClosedBox(long personRequestId);

    /// <summary>Every closed box, most recently closed first.</summary>
    IReadOnlyList<Box> GetBoxes();

    Box? FindBoxById(long id);

    /// <summary>Cases packed into a given closed box, in the same insertion order they had in the
    /// queue (TransferredAt, then Id) — this is the order the printed box listing uses.</summary>
    IReadOnlyList<PersonRequest> GetCasesByBoxId(long boxId);
}

public sealed class BoxRepository(string connectionString) : IBoxRepository
{
    public Box CloseBox(string code, DateTimeOffset closedAt)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var transaction = connection.BeginTransaction();

        long boxId;
        int number;
        var manualCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO Box (Number, Code, ClosedAt)
                VALUES ((SELECT COALESCE(MAX(Number), 0) + 1 FROM Box), $code, $closedAt);
                SELECT last_insert_rowid();
                """;
            insertCommand.Parameters.AddWithValue("$code", (object?)manualCode ?? string.Empty);
            insertCommand.Parameters.AddWithValue("$closedAt", closedAt.ToString("O"));
            boxId = (long)insertCommand.ExecuteScalar()!;
        }

        using (var numberCommand = connection.CreateCommand())
        {
            numberCommand.Transaction = transaction;
            numberCommand.CommandText = "SELECT Number, Code FROM Box WHERE Id = $id";
            numberCommand.Parameters.AddWithValue("$id", boxId);
            using var r = numberCommand.ExecuteReader();
            r.Read();
            number = r.GetInt32(0);
            var storedCode = r.GetString(1);
            if (string.IsNullOrWhiteSpace(storedCode))
            {
                manualCode = $"A{number}-CD";
                using var updateCodeCmd = connection.CreateCommand();
                updateCodeCmd.Transaction = transaction;
                updateCodeCmd.CommandText = "UPDATE Box SET Code = $code WHERE Id = $id";
                updateCodeCmd.Parameters.AddWithValue("$code", manualCode);
                updateCodeCmd.Parameters.AddWithValue("$id", boxId);
                updateCodeCmd.ExecuteNonQuery();
            }
            else
            {
                manualCode = storedCode;
            }
        }

        using (var assignCommand = connection.CreateCommand())
        {
            assignCommand.Transaction = transaction;
            assignCommand.CommandText = "UPDATE PersonRequest SET BoxId = $boxId WHERE Destination = 'Caja' AND BoxId IS NULL AND SinCarpeta = 0";
            assignCommand.Parameters.AddWithValue("$boxId", boxId);
            assignCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return new Box { Id = boxId, Number = number, Code = manualCode, ClosedAt = closedAt };
    }

    public void ReopenBox(long boxId)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var transaction = connection.BeginTransaction();

        // 1. Unpack all cases from this box back to the open queue (BoxId = NULL, Destination remains 'Caja')
        using (var unpackCommand = connection.CreateCommand())
        {
            unpackCommand.Transaction = transaction;
            unpackCommand.CommandText = "UPDATE PersonRequest SET BoxId = NULL WHERE BoxId = $boxId";
            unpackCommand.Parameters.AddWithValue("$boxId", boxId);
            unpackCommand.ExecuteNonQuery();
        }

        // 2. Delete the closed box record
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM Box WHERE Id = $boxId";
            deleteCommand.Parameters.AddWithValue("$boxId", boxId);
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void RemoveCaseFromClosedBox(long personRequestId)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PersonRequest
            SET Destination = 'None', BoxId = NULL, TransferredAt = NULL
            WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$id", personRequestId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Box> GetBoxes()
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Box ORDER BY Number DESC";
        using var reader = command.ExecuteReader();
        var results = new List<Box>();
        while (reader.Read())
        {
            results.Add(MapBox(reader));
        }
        return results;
    }

    public Box? FindBoxById(long id)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Box WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapBox(reader) : null;
    }

    public IReadOnlyList<PersonRequest> GetCasesByBoxId(long boxId)
    {
        using var connection = SqliteConnectionSetup.OpenConfigured(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM PersonRequest
            WHERE BoxId = $boxId
            ORDER BY TransferredAt, Id
            """;
        command.Parameters.AddWithValue("$boxId", boxId);
        using var reader = command.ExecuteReader();
        var results = new List<PersonRequest>();
        while (reader.Read())
        {
            results.Add(PersonRequestMapper.Map(reader));
        }
        return results;
    }

    private static Box MapBox(SqliteDataReader reader)
    {
        var codeOrdinal = reader.GetOrdinal("Code");
        var number = reader.GetInt32(reader.GetOrdinal("Number"));
        var code = !reader.IsDBNull(codeOrdinal) && !string.IsNullOrWhiteSpace(reader.GetString(codeOrdinal))
            ? reader.GetString(codeOrdinal)
            : $"A{number}-CD";

        return new Box
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Number = number,
            Code = code,
            ClosedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ClosedAt")))
        };
    }
}
