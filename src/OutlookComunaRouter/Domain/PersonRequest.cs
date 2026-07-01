namespace OutlookComunaRouter.Domain;

public enum RequestStatus
{
    Pending,
    Sent,
    Responded
}

public sealed class PersonRequest
{
    public long Id { get; set; }
    public string? FullName { get; set; }
    public string? Rut { get; set; }
    public string? Comuna { get; set; }
    public required string SourceMessageId { get; set; }
    public string? SourceConversationId { get; set; }
    public required string SourceSubject { get; set; }
    public required string SourceSender { get; set; }
    public bool NeedsReview { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTimeOffset? RequestSentAt { get; set; }
    public string? RequestMessageId { get; set; }
    public DateTimeOffset? ResponseReceivedAt { get; set; }
    public string? ResponseMessageId { get; set; }
    public string? LastFolderDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
