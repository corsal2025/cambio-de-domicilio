namespace CambioDeDomicilio.Domain;

public sealed record ComunaContact(string Comuna, string ContactEmail, string Domain);

public sealed record IncomingEmail(
    string MessageId,
    string ConversationId,
    string Subject,
    string SenderAddress,
    string BodyText,
    DateTimeOffset ReceivedAt);
