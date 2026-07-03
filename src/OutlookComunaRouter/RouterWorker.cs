using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Reporting;
using OutlookComunaRouter.Routing;

namespace OutlookComunaRouter;

public sealed class RouterWorker(
    AddressChangeRoutingService routingService,
    IEmailReader emailReader,
    IPersonRequestRepository repository,
    ICsvReportWriter reportWriter,
    RouterOptions options,
    ILogger<RouterWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim cycleGuard = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        repository.EnsureSchema();
        var interval = TimeSpan.FromMinutes(options.PollIntervalMinutes);

        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (!await cycleGuard.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            logger.LogWarning("Ciclo anterior aún en ejecución, se omite este tick");
            return;
        }

        try
        {
            var contacts = routingService.LoadDirectory();

            var incoming = await emailReader.GetMessagesInFolderAsync(options.SourceFolderName, cancellationToken);
            foreach (var email in incoming)
            {
                try
                {
                    routingService.ProcessIncomingRequest(email, contacts);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error procesando un correo de '{Folder}', se continúa con el resto del lote", options.SourceFolderName);
                }
            }

            var confirmations = await emailReader.GetMessagesInFolderAsync(options.ConfirmationFolderName, cancellationToken);
            foreach (var email in confirmations)
            {
                try
                {
                    routingService.ProcessUploadedCase(email);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error procesando un correo de '{Folder}', se continúa con el resto del lote", options.ConfirmationFolderName);
                }
            }

            reportWriter.Write(repository.GetAll(), options.ReportCsvPath);
            logger.LogInformation(
                "Ciclo completado: {IncomingCount} en '{SourceFolder}', {ConfirmationCount} en '{ConfirmationFolder}'",
                incoming.Count, options.SourceFolderName, confirmations.Count, options.ConfirmationFolderName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el ciclo de sondeo, se reintentará en el próximo tick");
        }
        finally
        {
            cycleGuard.Release();
        }
    }
}
