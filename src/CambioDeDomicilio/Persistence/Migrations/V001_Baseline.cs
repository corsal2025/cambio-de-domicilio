using Microsoft.Data.Sqlite;

namespace CambioDeDomicilio.Persistence.Migrations;

/// <summary>Version 1 — adopts every database that existed before schema versioning, and builds the
/// full schema on a fresh file. It is the previous <c>EnsureSchema</c> logic moved here verbatim
/// (including the legacy table rebuild and its hand-written column list), so it must stay frozen:
/// later schema changes are new migrations, never edits to this one.
/// The four data backfills below used to run on every startup; they now run once, here
/// (owner decision recorded in openspec/changes/refactor-persistence-migrations/design.md, D4).</summary>
internal sealed class V001_Baseline : IMigration
{
    public int Version => 1;

    public string Description => "Baseline schema: adopt pre-versioning databases and create the full schema";

    private static SqliteCommand NewCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        return command;
    }

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var discardedCommand = NewCommand(connection, transaction))
        {
            discardedCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS DiscardedEmail (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SourceMessageId TEXT NOT NULL UNIQUE,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    DiscardedAt TEXT NOT NULL
                );
                """;
            discardedCommand.ExecuteNonQuery();
        }

        using var command = NewCommand(connection, transaction);
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PersonRequest (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NULL,
                Rut TEXT NULL,
                Comuna TEXT NULL,
                -- Not UNIQUE: one source email can list several contributors, each getting its
                -- own row sharing the same SourceMessageId.
                SourceMessageId TEXT NOT NULL,
                SourceConversationId TEXT NULL,
                SourceSubject TEXT NOT NULL,
                SourceSender TEXT NOT NULL,
                NeedsReview INTEGER NOT NULL,
                Status TEXT NOT NULL,
                ReceivedAt TEXT NOT NULL,
                FechaUltimaCarpeta TEXT NULL,
                UploadedAt TEXT NULL,
                ConfirmedAt TEXT NULL,
                ConfirmedByUserId INTEGER NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);

            CREATE TABLE IF NOT EXISTS DeletedSourceMessage (
                SourceMessageId TEXT PRIMARY KEY,
                DeletedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ProcessedBounce (
                BounceMessageId TEXT PRIMARY KEY,
                ProcessedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Box (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Number INTEGER NOT NULL,
                Code TEXT NOT NULL DEFAULT '',
                ClosedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, transaction, "Marked", "Marked INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, transaction, "BoxId", "BoxId INTEGER NULL");
        EnsureColumnExists(connection, transaction, "SectorPdfGeneratedAt", "SectorPdfGeneratedAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "FolderNotFound", "FolderNotFound INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, transaction, "FolderNotFoundNotifiedAt", "FolderNotFoundNotifiedAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "CodigoF8", "CodigoF8 TEXT NULL");
        EnsureColumnExists(connection, transaction, "MovedToF8At", "MovedToF8At TEXT NULL");
        EnsureColumnExists(connection, transaction, "Destination", "Destination TEXT NOT NULL DEFAULT 'None'");
        EnsureColumnExists(connection, transaction, "TransferredAt", "TransferredAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "CertificadoNotifiedAt", "CertificadoNotifiedAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "PendienteCarpeta", "PendienteCarpeta INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, transaction, "MarkedAt", "MarkedAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "SinCarpeta", "SinCarpeta INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, transaction, "ConfirmationBouncedAt", "ConfirmationBouncedAt TEXT NULL");
        EnsureColumnExists(connection, transaction, "SoloCaja", "SoloCaja INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, transaction, "ClosedWithoutFolderAt", "ClosedWithoutFolderAt TEXT NULL");

        using (var updateLegacy = NewCommand(connection, transaction))
        {
            updateLegacy.CommandText = "UPDATE PersonRequest SET SoloCaja = 1 WHERE CodigoF8 IS NOT NULL AND Destination = 'None' AND FolderNotFound = 0;";
            updateLegacy.ExecuteNonQuery();
        }
        EnsureColumnExists(connection, transaction, "Code", "Code TEXT NOT NULL DEFAULT ''", "Box");
        RemoveSourceMessageIdUniqueConstraintIfPresent(connection, transaction);

        // Backfill migration: cases transferred under the old single-destination mechanism
        // (MovedToF8At) must be recognized under the new generic Destination/TransferredAt
        // mechanism, or they'd silently disappear from the F8 page. MovedToF8At itself is left
        // in place (not dropped) per this project's additive-schema convention. Safe/idempotent:
        // on a fresh database MovedToF8At is always NULL, so the UPDATE affects zero rows.
        using (var backfillCommand = NewCommand(connection, transaction))
        {
            backfillCommand.CommandText = """
                UPDATE PersonRequest SET Destination = 'F8', TransferredAt = MovedToF8At
                WHERE MovedToF8At IS NOT NULL AND Destination = 'None'
                """;
            backfillCommand.ExecuteNonQuery();
        }

        // The Certificado destination was removed. Any case still stored under it is sent back to
        // Casos (Destination reset, TransferredAt cleared) so it doesn't get orphaned off every
        // screen, and so Map's Enum.Parse<CaseDestination> never hits the now-undefined value.
        // The CertificadoNotifiedAt column is left in place per this project's additive-schema
        // convention. Safe/idempotent: affects zero rows on any database that never used it.
        using (var dropCertificadoCommand = NewCommand(connection, transaction))
        {
            dropCertificadoCommand.CommandText =
                "UPDATE PersonRequest SET Destination = 'None', TransferredAt = NULL WHERE Destination = 'Certificado'";
            dropCertificadoCommand.ExecuteNonQuery();
        }

        // Cases that were already marked as Uploaded or Confirmed in Casos are migrated to the Subidas destination
        using (var migrateSubidasCommand = NewCommand(connection, transaction))
        {
            migrateSubidasCommand.CommandText = """
                UPDATE PersonRequest
                SET Destination = 'Subidas',
                    TransferredAt = COALESCE(ConfirmedAt, UploadedAt, CURRENT_TIMESTAMP)
                WHERE Destination = 'None'
                  AND Status IN ('Uploaded', 'Confirmed')
                  AND ClosedWithoutFolderAt IS NULL;
                """;
            migrateSubidasCommand.ExecuteNonQuery();
        }

        // Cases closed without folder or flagged as SinCarpeta in F8 belong to the SinCarpetas destination so they do not leak into Casos or F8
        using (var migrateSinCarpetasCommand = NewCommand(connection, transaction))
        {
            migrateSinCarpetasCommand.CommandText = """
                UPDATE PersonRequest
                SET Destination = 'SinCarpetas',
                    ClosedWithoutFolderAt = COALESCE(ClosedWithoutFolderAt, TransferredAt, CURRENT_TIMESTAMP),
                    TransferredAt = COALESCE(TransferredAt, ClosedWithoutFolderAt, CURRENT_TIMESTAMP),
                    SinCarpeta = 1
                WHERE (ClosedWithoutFolderAt IS NOT NULL OR (SinCarpeta = 1 AND Destination = 'F8') OR Destination = 'SinCarpetas')
                  AND (Destination != 'SinCarpetas' OR ClosedWithoutFolderAt IS NULL OR TransferredAt IS NULL OR SinCarpeta != 1);
                """;
            migrateSinCarpetasCommand.ExecuteNonQuery();
        }
    }

    /// <summary>Additive migration for databases created before multiple contributors per email were
    /// supported, where SourceMessageId was still UNIQUE. SQLite can't drop a column constraint
    /// directly, so this rebuilds the table when the old constraint is detected.</summary>
    private static void RemoveSourceMessageIdUniqueConstraintIfPresent(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var checkCommand = NewCommand(connection, transaction))
        {
            checkCommand.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='PersonRequest'";
            var tableSql = checkCommand.ExecuteScalar() as string;
            if (tableSql is null || !tableSql.Contains("SourceMessageId TEXT NOT NULL UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return; // already migrated, or a fresh install that never had the constraint
            }
        }

        using (var rebuildCommand = NewCommand(connection, transaction))
        {
            rebuildCommand.CommandText = """
                CREATE TABLE PersonRequest_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    Marked INTEGER NOT NULL DEFAULT 0,
                    SectorPdfGeneratedAt TEXT NULL,
                    FolderNotFound INTEGER NOT NULL DEFAULT 0,
                    FolderNotFoundNotifiedAt TEXT NULL,
                    CodigoF8 TEXT NULL,
                    MovedToF8At TEXT NULL,
                    Destination TEXT NOT NULL DEFAULT 'None',
                    TransferredAt TEXT NULL,
                    CertificadoNotifiedAt TEXT NULL,
                    PendienteCarpeta INTEGER NOT NULL DEFAULT 0,
                    MarkedAt TEXT NULL,
                    SinCarpeta INTEGER NOT NULL DEFAULT 0,
                    ConfirmationBouncedAt TEXT NULL,
                    BoxId INTEGER NULL,
                    SoloCaja INTEGER NOT NULL DEFAULT 0,
                    ClosedWithoutFolderAt TEXT NULL
                );
                INSERT INTO PersonRequest_new
                    (Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                     NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                     FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta, ConfirmationBouncedAt, BoxId, SoloCaja, ClosedWithoutFolderAt)
                SELECT
                    Id, FullName, Rut, Comuna, SourceMessageId, SourceConversationId, SourceSubject, SourceSender,
                    NeedsReview, Status, ReceivedAt, FechaUltimaCarpeta, UploadedAt, ConfirmedAt, ConfirmedByUserId, CreatedAt, Marked, SectorPdfGeneratedAt,
                    FolderNotFound, FolderNotFoundNotifiedAt, CodigoF8, MovedToF8At, Destination, TransferredAt, CertificadoNotifiedAt, PendienteCarpeta, MarkedAt, SinCarpeta, ConfirmationBouncedAt, BoxId, SoloCaja, ClosedWithoutFolderAt
                FROM PersonRequest;
                DROP TABLE PersonRequest;
                ALTER TABLE PersonRequest_new RENAME TO PersonRequest;
                CREATE INDEX IF NOT EXISTS IX_PersonRequest_RutComuna ON PersonRequest (Rut, Comuna);
                """;
            rebuildCommand.ExecuteNonQuery();
        }
    }

    /// <summary>Additive migration for columns added after the table was first created — SQLite has
    /// no "ADD COLUMN IF NOT EXISTS", so check PRAGMA table_info first.</summary>
    private static void EnsureColumnExists(SqliteConnection connection, SqliteTransaction transaction, string columnName, string columnDefinitionSql, string tableName = "PersonRequest")
    {
        using (var pragmaCommand = NewCommand(connection, transaction))
        {
            pragmaCommand.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = pragmaCommand.ExecuteReader();
            var nameOrdinal = reader.GetOrdinal("name");
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(nameOrdinal), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterCommand = NewCommand(connection, transaction);
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnDefinitionSql}";
        alterCommand.ExecuteNonQuery();
    }
}
