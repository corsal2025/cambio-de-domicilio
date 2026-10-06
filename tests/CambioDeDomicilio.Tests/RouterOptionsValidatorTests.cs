using CambioDeDomicilio.Configuration;
using Xunit;

namespace CambioDeDomicilio.Tests;

public class RouterOptionsValidatorTests
{
    private static RouterOptions ValidOptions() => new()
    {
        Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "svc", Password = "secret" },
        MailboxAddress = "cambiodedomicilio@munivalpo.cl",
        OwnDomain = "munivalpo.cl",
        SqliteDbPath = "data/router.db",
        ComunaDirectoryCsvPath = "data/comunas.csv",
        ReportCsvPath = "data/reporte.csv",
        NotificationEmailAddress = "ops@example.com"
    };

    [Fact]
    public void Validate_CompleteOptions_ReturnsNoProblems()
    {
        var result = RouterOptionsValidator.Validate(ValidOptions());

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_EmptyCredentials_IsOnlyAWarningSoDevelopmentWithoutMailboxStillStarts()
    {
        var options = ValidOptions();
        options.Ews.Username = "";
        options.Ews.Password = "";

        var result = RouterOptionsValidator.Validate(options);

        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, w => w.Contains("Router:Ews:Username"));
        Assert.Contains(result.Warnings, w => w.Contains("Router:Ews:Password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://mail.munivalpo.cl/EWS")]
    public void Validate_InvalidEwsUrl_IsAnError(string url)
    {
        var options = ValidOptions();
        options.Ews.Url = url;

        Assert.Contains(RouterOptionsValidator.Validate(options).Errors, e => e.Contains("Router:Ews:Url"));
    }

    [Fact]
    public void Validate_SameSourceAndConfirmationFolder_IsAnError()
    {
        var options = ValidOptions();
        options.ConfirmationFolderName = options.SourceFolderName;

        Assert.Contains(RouterOptionsValidator.Validate(options).Errors, e => e.Contains("ConfirmationFolderName"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_NonPositiveDeadline_IsAnError(int days)
    {
        var options = ValidOptions();
        options.PlazoDiasHabiles = days;

        Assert.Contains(RouterOptionsValidator.Validate(options).Errors, e => e.Contains("PlazoDiasHabiles"));
    }

    [Fact]
    public void Validate_MissingPaths_AreErrors()
    {
        var options = ValidOptions();
        options.SqliteDbPath = " ";
        options.ComunaDirectoryCsvPath = "";

        var errors = RouterOptionsValidator.Validate(options).Errors;

        Assert.Contains(errors, e => e.Contains("SqliteDbPath"));
        Assert.Contains(errors, e => e.Contains("ComunaDirectoryCsvPath"));
    }
}
