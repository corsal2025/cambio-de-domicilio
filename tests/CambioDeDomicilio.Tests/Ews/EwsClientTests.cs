using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Ews;
using Xunit;

namespace CambioDeDomicilio.Tests.Ews;

public class EwsClientTests
{
    private static RouterOptions BuildOptions() => new()
    {
        Ews = new EwsOptions { Url = "https://ews.example.com/EWS/Exchange.asmx", Username = "user", Password = "pass" },
        MailboxAddress = "mailbox@example.com",
        OwnDomain = "example.com",
        SqliteDbPath = "unused.db",
        ComunaDirectoryCsvPath = "unused.csv",
        ReportCsvPath = "unused-report.csv",
        NotificationEmailAddress = "notify@example.com"
    };

    private sealed class StubHandler(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(CallCount));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_PermanentHttpError_FailsImmediatelyWithoutRetry(HttpStatusCode statusCode)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));
        using var client = new EwsClient(BuildOptions(), NullLogger<EwsClient>.Instance, handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync("<soap/>", CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_TransientHttpError_RetriesBeforeFailing()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new EwsClient(BuildOptions(), NullLogger<EwsClient>.Instance, handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync("<soap/>", CancellationToken.None));

        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_ReturnsParsedDocument()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<root/>")
        });
        using var client = new EwsClient(BuildOptions(), NullLogger<EwsClient>.Instance, handler);

        var result = await client.SendAsync("<soap/>", CancellationToken.None);

        Assert.Equal("root", result.Root!.Name.LocalName);
        Assert.Equal(1, handler.CallCount);
    }
}
