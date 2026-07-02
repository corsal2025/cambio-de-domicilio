using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Routing;
using Xunit;

namespace OutlookComunaRouter.Tests.Routing;

public class AddressChangeRoutingServiceTests : IDisposable
{
    private static readonly IReadOnlyList<ComunaContact> Contacts =
    [
        new ComunaContact("Catemu", "rfloresc@municatemu.cl", "municatemu.cl")
    ];

    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"routing-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly FakeMailSender mailSender = new();
    private readonly FakeNotificationChannel notificationChannel = new();
    private readonly AddressChangeRoutingService sut;

    public AddressChangeRoutingServiceTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();

        var options = new RouterOptions
        {
            Ews = new EwsOptions
            {
                Url = "https://ews.example.invalid/EWS/Exchange.asmx",
                Username = "test-user",
                Password = "test-password"
            },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        sut = new AddressChangeRoutingService(
            repository,
            new ComunaDirectory(),
            mailSender,
            [notificationChannel],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);
    }

    [Fact]
    public async Task ProcessNotificationAsync_KnownComunaValidData_SendsAndMarksSent()
    {
        var email = NewNotification("msg-1", "conv-1", "rfloresc@municatemu.cl",
            "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7");

        await sut.ProcessNotificationAsync(email, Contacts, CancellationToken.None);

        Assert.Single(mailSender.SentMessages);
        var stored = repository.GetAll().Single();
        Assert.Equal(RequestStatus.Sent, stored.Status);
    }

    [Fact]
    public async Task ProcessNotificationAsync_SecondNotificationSameRutComuna_DoesNotSendAgain()
    {
        var first = NewNotification("msg-1", "conv-1", "rfloresc@municatemu.cl",
            "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7");
        var duplicate = NewNotification("msg-2", "conv-2", "rfloresc@municatemu.cl",
            "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7");

        await sut.ProcessNotificationAsync(first, Contacts, CancellationToken.None);
        await sut.ProcessNotificationAsync(duplicate, Contacts, CancellationToken.None);

        Assert.Single(mailSender.SentMessages); // only the first one was sent
        Assert.Equal(2, repository.GetAll().Count); // both source emails recorded
    }

    [Fact]
    public void ProcessPotentialReply_SameThread_MarksRespondedAndNotifies()
    {
        var sent = InsertSentRequest(conversationId: "conv-1");
        var reply = NewNotification("reply-1", "conv-1", "rfloresc@municatemu.cl", "Se adjunta la última carpeta.");

        sut.ProcessPotentialReply(reply, Contacts);

        var updated = repository.GetAll().Single(r => r.Id == sent.Id);
        Assert.Equal(RequestStatus.Responded, updated.Status);
        Assert.Single(notificationChannel.Notified);
    }

    [Fact]
    public void ProcessPotentialReply_NewThreadWithMatchingRut_MarksRespondedAndNotifies()
    {
        var sent = InsertSentRequest(conversationId: "conv-original");
        var reply = NewNotification("reply-1", "conv-different", "rfloresc@municatemu.cl",
            "Junto con saludar, se adjunta carpeta de RUT: 18.785.387-7");

        sut.ProcessPotentialReply(reply, Contacts);

        var updated = repository.GetAll().Single(r => r.Id == sent.Id);
        Assert.Equal(RequestStatus.Responded, updated.Status);
        Assert.Single(notificationChannel.Notified);
    }

    [Fact]
    public void ProcessPotentialReply_NoMatch_DoesNotNotify()
    {
        InsertSentRequest(conversationId: "conv-1");
        var unrelated = NewNotification("reply-1", "conv-other", "rfloresc@municatemu.cl", "Correo sin relación.");

        sut.ProcessPotentialReply(unrelated, Contacts);

        Assert.Empty(notificationChannel.Notified);
    }

    [Fact]
    public void ProcessPotentialReply_UnknownDomain_IsIgnored()
    {
        InsertSentRequest(conversationId: "conv-1");
        var fromUnknownDomain = NewNotification("reply-1", "conv-1", "alguien@otracomuna.cl", "Respuesta.");

        sut.ProcessPotentialReply(fromUnknownDomain, Contacts);

        Assert.Empty(notificationChannel.Notified);
    }

    private PersonRequest InsertSentRequest(string conversationId)
    {
        var request = new PersonRequest
        {
            FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
            Rut = "18.785.387-7",
            Comuna = "Catemu",
            SourceMessageId = $"original-{Guid.NewGuid():N}",
            SourceConversationId = conversationId,
            SourceSubject = "Cambio de domicilio",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = false,
            Status = RequestStatus.Pending
        };
        var id = repository.Insert(request);
        repository.UpdateStatusToSent(id, "n/a", DateTimeOffset.UtcNow);
        request.Id = id;
        return request;
    }

    private static IncomingEmail NewNotification(string messageId, string conversationId, string sender, string body) =>
        new(messageId, conversationId, "Cambio de domicilio", sender, body, DateTimeOffset.UtcNow);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    private sealed class FakeMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> SentMessages { get; } = [];

        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
        {
            SentMessages.Add((toAddress, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotificationChannel : INotificationChannel
    {
        public List<(string FullName, string Rut, string Comuna)> Notified { get; } = [];

        public void NotifyResponded(string fullName, string rut, string comuna) =>
            Notified.Add((fullName, rut, comuna));
    }
}
