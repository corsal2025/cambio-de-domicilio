using OutlookComunaRouter;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Graph;
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
builder.Services.AddSingleton<IGraphClientFactory, GraphClientFactory>();
builder.Services.AddSingleton<IEmailReader, EmailReader>();
builder.Services.AddSingleton<IMailSender, MailSender>();
builder.Services.AddSingleton<ICsvReportWriter, CsvReportWriter>();
builder.Services.AddSingleton<AddressChangeRoutingService>();

builder.Services.AddSingleton<INotificationChannel, WindowsToastNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();

builder.Services.AddHostedService<RouterWorker>();

var host = builder.Build();
host.Run();
