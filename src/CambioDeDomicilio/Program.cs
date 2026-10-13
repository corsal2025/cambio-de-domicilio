using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using CambioDeDomicilio;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Ews;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Persistence.Migrations;
using CambioDeDomicilio.Reporting;
using CambioDeDomicilio.Routing;

// Task Scheduler fires both an AtLogOn and an AtStartup trigger, and also auto-restarts the
// process on crash — any of those can overlap with a second copy already running. Bail out
// immediately if another instance already holds the mutex instead of racing it.
// The mutex is per environment: a Development run from VS Code (its own ports, DB copy and no
// mailbox, see .vscode/launch.json) must be able to coexist with the production instance.
var hostEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";
var mutexName = hostEnvironment == "Production"
    ? "Global\\CambioDeDomicilio.SingleInstance"
    : $"Global\\CambioDeDomicilio.SingleInstance.{hostEnvironment}";
using var singleInstanceMutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var isFirstInstance);
if (!isFirstInstance)
{
    Console.WriteLine("CambioDeDomicilio ya está corriendo. Cerrando esta instancia duplicada.");
    return;
}

// ContentRootPath pinned to the exe's own folder (not the process's current directory) so every
// relative path in config (SqliteDbPath, ComunaDirectoryCsvPath, cert path, etc.) resolves the
// same way no matter how the app is launched — double-click, a shortcut, Task Scheduler, or a
// pendrive that gets a different drive letter on every PC it's plugged into.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var routerOptions = builder.Configuration.GetSection(RouterOptions.SectionName).Get<RouterOptions>()
    ?? throw new InvalidOperationException($"Missing '{RouterOptions.SectionName}' configuration section.");
var optionsValidation = RouterOptionsValidator.Validate(routerOptions);
if (optionsValidation.Errors.Count > 0)
{
    throw new InvalidOperationException(
        $"Invalid '{RouterOptions.SectionName}' configuration:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", optionsValidation.Errors)}");
}
builder.Services.AddSingleton(routerOptions);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IPersonRequestRepository>(_ =>
    new PersonRequestRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IDiscardedEmailRepository>(_ =>
    new DiscardedEmailRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IMessageTombstoneRepository>(_ =>
    new MessageTombstoneRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IBoxRepository>(_ =>
    new BoxRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IComunaDirectory, ComunaDirectory>();
builder.Services.AddSingleton<IEwsClient, EwsClient>();
builder.Services.AddSingleton<EwsEmailReader>();
builder.Services.AddSingleton<IEmailReader>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IEmailMover>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IMailSender, EwsMailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();
builder.Services.AddSingleton<CambioDeDomicilio.Statistics.StatisticsService>();
builder.Services.AddSingleton<CambioDeDomicilio.Search.GlobalSearchService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddSingleton<RouterWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RouterWorker>());

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "keys")))
    .SetApplicationName("CambioDeDomicilio");

builder.Services.AddRazorPages(options => options.RootDirectory = "/Dashboard/Pages");

var app = builder.Build();

foreach (var warning in optionsValidation.Warnings)
{
    app.Logger.LogWarning("{ConfigurationWarning}", warning);
}

// Deployment verification mode: reads the mailbox through the real EWS pipeline and
// prints only counts and sender domains (no personal data), then exits.
if (args.Contains("--smoke-test"))
{
    var reader = app.Services.GetRequiredService<IEmailReader>();
    var messages = await reader.GetMessagesInFolderAsync(routerOptions.SourceFolderName, CancellationToken.None);

    Console.WriteLine($"Smoke test OK: {messages.Count} message(s) in '{routerOptions.SourceFolderName}'");
    foreach (var group in messages.GroupBy(m => m.SenderAddress[(m.SenderAddress.LastIndexOf('@') + 1)..]))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()} message(s)");
    }
    return;
}


// Upgrade the database before anything can read or write it (the browser launch below and the
// listeners only start at app.Run). Failure throws and stops startup: never serve a half-upgraded
// database. Runs after the smoke test, which promises no side effects.
var migration = SchemaMigrator.ForApplication($"Data Source={routerOptions.SqliteDbPath}").Migrate();
if (migration.AppliedVersions.Count > 0)
{
    app.Logger.LogInformation(
        "Database upgraded from schema v{From} to v{To}; backup: {BackupPath}",
        migration.StartVersion, migration.FinalVersion, migration.BackupPath ?? "(none, new database)");
}

_ = Task.Run(async () =>
{
    try
    {
        // Small delay to let the server bind before opening the browser
        await Task.Delay(2000);
        var url = app.Configuration["DashboardUrl"] ?? "https://localhost:5001";
        Console.WriteLine($"Abriendo dashboard: {url}");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el navegador automáticamente: {ex.Message}");
        Console.WriteLine($"Abre {app.Configuration["DashboardUrl"] ?? "https://localhost:5001"} manualmente en tu navegador.");
    }
});

// The dashboard listens on a single HTTPS endpoint (see Kestrel:Endpoints in appsettings.json); there is
// no plain-HTTP listener, so no HTTPS redirection middleware is needed.
app.UseStaticFiles();
app.MapRazorPages();

app.Run();
