using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Dashboard.Auth;
using OutlookComunaRouter.Dashboard.Pages;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Pages;

public class ResetPasswordModelTests
{
    [Fact]
    public void OnPost_ValidTokenAndMatchingPasswords_CompletesReset()
    {
        var service = new FakePasswordResetService(PasswordResetCompletionOutcome.Success);
        var model = NewModel(service, token: "abc123", newPassword: "NuevaClave2027#", confirmPassword: "NuevaClave2027#");

        model.OnPost();

        Assert.Equal("abc123", service.CompletedToken);
        Assert.Equal("NuevaClave2027#", service.CompletedPassword);
        Assert.True(model.Completed);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPost_PasswordsDoNotMatch_RejectsWithoutCallingService()
    {
        var service = new FakePasswordResetService(PasswordResetCompletionOutcome.Success);
        var model = NewModel(service, token: "abc123", newPassword: "NuevaClave2027#", confirmPassword: "OtraCosa#");

        model.OnPost();

        Assert.Null(service.CompletedToken);
        Assert.True(model.MessageIsError);
        Assert.False(model.Completed);
    }

    [Theory]
    [InlineData(PasswordResetCompletionOutcome.InvalidOrExpiredToken)]
    [InlineData(PasswordResetCompletionOutcome.TokenAlreadyUsed)]
    [InlineData(PasswordResetCompletionOutcome.PasswordTooShort)]
    public void OnPost_ServiceRejects_ShowsErrorAndDoesNotCompleteFlow(PasswordResetCompletionOutcome outcome)
    {
        var service = new FakePasswordResetService(outcome);
        var model = NewModel(service, token: "abc123", newPassword: "NuevaClave2027#", confirmPassword: "NuevaClave2027#");

        model.OnPost();

        Assert.True(model.MessageIsError);
        Assert.False(model.Completed);
    }

    private static ResetPasswordModel NewModel(IPasswordResetService service, string token, string newPassword, string confirmPassword) =>
        new(service)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Token = token,
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword
        };

    private sealed class FakePasswordResetService(PasswordResetCompletionOutcome outcome) : IPasswordResetService
    {
        public string? CompletedToken { get; private set; }
        public string? CompletedPassword { get; private set; }

        public Task<PasswordResetRequestOutcome> RequestResetAsync(string username, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public PasswordResetCompletionOutcome CompleteReset(string token, string newPassword)
        {
            CompletedToken = token;
            CompletedPassword = newPassword;
            return outcome;
        }
    }
}
