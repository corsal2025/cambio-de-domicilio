using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using OutlookComunaRouter;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Dashboard.Auth;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Ews;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Reporting;
using OutlookComunaRouter.Routing;

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
builder.Services.AddSingleton<IUserRepository>(_ =>
    new UserRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IPasswordResetTokenRepository>(_ =>
    new PasswordResetTokenRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<ILoginService, LoginService>();
builder.Services.AddSingleton<IPasswordResetService, PasswordResetService>();
builder.Services.AddSingleton<IComunaDirectory, ComunaDirectory>();
builder.Services.AddSingleton<IEwsClient, EwsClient>();
builder.Services.AddSingleton<EwsEmailReader>();
builder.Services.AddSingleton<IEmailReader>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IEmailMover>(sp => sp.GetRequiredService<EwsEmailReader>());
builder.Services.AddSingleton<IMailSender, EwsMailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();
builder.Services.AddSingleton<OutlookComunaRouter.Statistics.StatisticsService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddSingleton<RouterWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RouterWorker>());

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // dashboard is HTTPS-only
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options => options.RootDirectory = "/Dashboard/Pages");

var app = builder.Build();

// One-time admin CLI commands, run instead of starting the host.
if (args.Contains("--add-user") || args.Contains("--remove-user"))
{
    var users = app.Services.GetRequiredService<IUserRepository>();
    users.EnsureSchema();

    if (args.Contains("--add-user"))
    {
        var username = args[Array.IndexOf(args, "--add-user") + 1];
        var password = Environment.GetEnvironmentVariable("OCR_ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            Console.Write("Contraseña: ");
            password = ReadPasswordMasked();
        }
        var (hash, salt, iterations) = PasswordHasher.Hash(password);
        users.Insert(new DashboardUser { Username = username, PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        Console.WriteLine($"Usuario '{username}' creado.");
    }
    else
    {
        var username = args[Array.IndexOf(args, "--remove-user") + 1];
        users.Delete(username);
        Console.WriteLine($"Usuario '{username}' eliminado.");
    }

    return;
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

app.Services.GetRequiredService<IUserRepository>().EnsureSchema();
app.Services.GetRequiredService<IPasswordResetTokenRepository>().EnsureSchema();
app.Services.GetRequiredService<IDiscardedEmailRepository>().EnsureSchema();

_ = Task.Run(async () =>
{
    try
    {
        // Small delay to let the server bind before opening the browser
        await Task.Delay(2000);
        var url = "https://localhost:5001";
        Console.WriteLine($"Abriendo dashboard: {url}");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el navegador automáticamente: {ex.Message}");
        Console.WriteLine("Abre https://localhost:5001 manualmente en tu navegador.");
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
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

static string ReadPasswordMasked()
{
    var password = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Remove(password.Length - 1, 1);
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    return password.ToString();
}
