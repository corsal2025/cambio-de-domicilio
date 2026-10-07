using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Persistence;

/// <summary>Row-to-entity mapping shared by every repository that returns cases.</summary>
internal static class PersonRequestMapper
{
    // Every query selects "*" and the schema migrations guarantee all of these columns exist, so a missing
    // column or an unparseable value is a real bug and must surface instead of silently becoming
    // null/false (which would hide corrupt rows from the operator).
    public static PersonRequest Map(SqliteDataReader reader)
    {
        long? GetNullableInt64(string name)
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? null : reader.GetInt64(ord);
        }

        DateTimeOffset? GetNullableDateTimeOffset(string name)
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? null : DateTimeOffset.Parse(reader.GetString(ord));
        }

        bool GetBoolean(string name)
        {
            var ord = reader.GetOrdinal(name);
            return !reader.IsDBNull(ord) && reader.GetInt32(ord) == 1;
        }

        return new PersonRequest
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName")),
            Rut = reader.IsDBNull(reader.GetOrdinal("Rut")) ? null : reader.GetString(reader.GetOrdinal("Rut")),
            Comuna = reader.IsDBNull(reader.GetOrdinal("Comuna")) ? null : reader.GetString(reader.GetOrdinal("Comuna")),
            SourceMessageId = reader.GetString(reader.GetOrdinal("SourceMessageId")),
            SourceConversationId = reader.IsDBNull(reader.GetOrdinal("SourceConversationId")) ? null : reader.GetString(reader.GetOrdinal("SourceConversationId")),
            SourceSubject = reader.GetString(reader.GetOrdinal("SourceSubject")),
            SourceSender = reader.GetString(reader.GetOrdinal("SourceSender")),
            NeedsReview = reader.GetInt32(reader.GetOrdinal("NeedsReview")) == 1,
            Status = Enum.Parse<RequestStatus>(reader.GetString(reader.GetOrdinal("Status"))),
            ReceivedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ReceivedAt"))),
            FechaUltimaCarpeta = reader.IsDBNull(reader.GetOrdinal("FechaUltimaCarpeta")) ? null : DateOnly.Parse(reader.GetString(reader.GetOrdinal("FechaUltimaCarpeta"))),
            UploadedAt = reader.IsDBNull(reader.GetOrdinal("UploadedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UploadedAt"))),
            ConfirmedAt = reader.IsDBNull(reader.GetOrdinal("ConfirmedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ConfirmedAt"))),
            ConfirmedByUserId = reader.IsDBNull(reader.GetOrdinal("ConfirmedByUserId")) ? null : reader.GetInt64(reader.GetOrdinal("ConfirmedByUserId")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            Marked = reader.GetInt32(reader.GetOrdinal("Marked")) == 1,
            MarkedAt = reader.IsDBNull(reader.GetOrdinal("MarkedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("MarkedAt"))),
            SinCarpeta = reader.GetInt32(reader.GetOrdinal("SinCarpeta")) == 1,
            SectorPdfGeneratedAt = reader.IsDBNull(reader.GetOrdinal("SectorPdfGeneratedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("SectorPdfGeneratedAt"))),
            FolderNotFound = reader.GetInt32(reader.GetOrdinal("FolderNotFound")) == 1,
            PendienteCarpeta = reader.GetInt32(reader.GetOrdinal("PendienteCarpeta")) == 1,
            CodigoF8 = reader.IsDBNull(reader.GetOrdinal("CodigoF8")) ? null : reader.GetString(reader.GetOrdinal("CodigoF8")),
            Destination = Enum.Parse<CaseDestination>(reader.GetString(reader.GetOrdinal("Destination"))),
            TransferredAt = reader.IsDBNull(reader.GetOrdinal("TransferredAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("TransferredAt"))),
            ConfirmationBouncedAt = reader.IsDBNull(reader.GetOrdinal("ConfirmationBouncedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ConfirmationBouncedAt"))),
            BoxId = GetNullableInt64("BoxId"),
            SoloCaja = GetBoolean("SoloCaja"),
            ClosedWithoutFolderAt = GetNullableDateTimeOffset("ClosedWithoutFolderAt")
        };
    }
}
