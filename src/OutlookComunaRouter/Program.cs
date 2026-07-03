using OutlookComunaRouter;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Ews;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Reporting;
using OutlookComunaRouter.Routing;

var builder = Host.CreateApplicationBuilder(args);

var routerOptions = builder.Configuration.GetSection(RouterOptions.SectionName).Get<RouterOptions>()
    ?? throw new InvalidOperationException($"Missing '{RouterOptions.SectionName}' configuration section.");
builder.Services.AddSingleton(routerOptions);

builder.Services.AddSingleton<IPersonRequestRepository>(_ =>
    new PersonRequestRepository($"Data Source={routerOptions.SqliteDbPath}"));
builder.Services.AddSingleton<IComunaDirectory, ComunaDirectory>();
builder.Services.AddSingleton<IEwsClient, EwsClient>();
builder.Services.AddSingleton<IEmailReader, EwsEmailReader>();
builder.Services.AddSingleton<IMailSender, EwsMailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddHostedService<RouterWorker>();

var host = builder.Build();

// Deployment verification mode: reads the mailbox through the real EWS pipeline and
// prints only counts and sender domains (no personal data), then exits.
if (args.Contains("--smoke-test"))
{
    var reader = host.Services.GetRequiredService<IEmailReader>();
    var messages = await reader.GetMessagesInFolderAsync(routerOptions.SourceFolderName, CancellationToken.None);

    Console.WriteLine($"Smoke test OK: {messages.Count} message(s) in '{routerOptions.SourceFolderName}'");
    foreach (var group in messages.GroupBy(m => m.SenderAddress[(m.SenderAddress.LastIndexOf('@') + 1)..]))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()} message(s)");
    }
    return;
}

host.Run();
