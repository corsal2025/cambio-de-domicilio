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

var builder = WebApplication.CreateBuilder(args);

var routerOptions = builder.Configuration.GetSection(RouterOptions.SectionName).Get<RouterOptions>()
    ?? throw new InvalidOperationException($"Missing '{RouterOptions.SectionName}' configuration section.");
builder.Services.AddSingleton(routerOptions);

builder.Services.AddSingleton<IPersonRequestRepository>(_ =>
    new PersonRequestRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IUserRepository>(_ =>
    new UserRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<ILoginService, LoginService>();
builder.Services.AddSingleton<IComunaDirectory, ComunaDirectory>();
builder.Services.AddSingleton<IEwsClient, EwsClient>();
builder.Services.AddSingleton<IEmailReader, EwsEmailReader>();
builder.Services.AddSingleton<IMailSender, EwsMailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddHostedService<RouterWorker>();

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
        Console.Write("Contraseña: ");
        var password = ReadPasswordMasked();
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

app.UseHttpsRedirection(); // the plain-HTTP listener only ever redirects, never serves data
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
