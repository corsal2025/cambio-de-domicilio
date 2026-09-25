using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using CambioDeDomicilio;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Ews;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
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
builder.Services.AddSingleton(routerOptions);

builder.Services.AddSingleton<IPersonRequestRepository>(_ =>
    new PersonRequestRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IDiscardedEmailRepository>(_ =>
    new DiscardedEmailRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IComunaDirectory, ComunaDirectory>();
builder.Services.AddSingleton<IEwsClient, EwsClient>();
builder.Services.AddSingleton<EwsEmailReader>();
builder.Services.AddSingleton<IEmailReader>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IEmailMover>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IMailSender, EwsMailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();
builder.Services.AddSingleton<CambioDeDomicilio.Statistics.StatisticsService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddSingleton<RouterWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RouterWorker>());

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "keys")))
    .SetApplicationName("CambioDeDomicilio");

builder.Services.AddRazorPages(options => options.RootDirectory = "/Dashboard/Pages");

var app = builder.Build();

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

app.Services.GetRequiredService<IPersonRequestRepository>().EnsureSchema();
app.Services.GetRequiredService<IDiscardedEmailRepository>().EnsureSchema();

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

// Port 5002 is loopback-only (127.0.0.1, see appsettings.json) and exists solely so a local
// port-forwarding tool (e.g. a VS Code Dev Tunnel) can reach real content over plain HTTP — those
// tools proxy to a local HTTP port, not TLS, and TLS-terminate themselves at their own public
// HTTPS endpoint. Since 5002 never leaves the machine, skipping the redirect there doesn't expose
// anything on the LAN; the public-facing HTTP listener (port 5000, reachable on 0.0.0.0) still
// only ever redirects, never serves data.
app.UseWhen(
    context => context.Connection.LocalPort != 5002,
    branch => branch.UseHttpsRedirection());
app.UseStaticFiles();
app.MapRazorPages();

app.Run();
