using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Dashboard.Auth;
using CambioDeDomicilio.Dashboard.Pages;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class ForgotPasswordModelTests
{
    [Theory]
    [InlineData(PasswordResetRequestOutcome.Requested)]
    [InlineData(PasswordResetRequestOutcome.UserNotFound)]
    [InlineData(PasswordResetRequestOutcome.NoEmailOnFile)]
    public async Task OnPostAsync_AnyOutcome_ShowsSameGenericMessage(PasswordResetRequestOutcome outcome)
    {
        var service = new FakePasswordResetService(outcome);
        var model = new ForgotPasswordModel(service)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Username = "operador"
        };

        await model.OnPostAsync();

        Assert.Equal("operador", service.RequestedUsername);
        Assert.Equal(ForgotPasswordModel.GenericMessage, model.Message);
    }

    private sealed class FakePasswordResetService(PasswordResetRequestOutcome outcome) : IPasswordResetService
    {
        public string? RequestedUsername { get; private set; }

        public Task<PasswordResetRequestOutcome> RequestResetAsync(string username, CancellationToken cancellationToken)
        {
            RequestedUsername = username;
            return Task.FromResult(outcome);
        }

        public PasswordResetCompletionOutcome CompleteReset(string token, string newPassword) =>
            throw new NotSupportedException();
    }
}
