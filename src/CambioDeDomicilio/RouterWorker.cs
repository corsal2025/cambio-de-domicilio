using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Reporting;
using CambioDeDomicilio.Routing;

namespace CambioDeDomicilio;

public sealed record SyncCycleResult(
    bool Success,
    bool AlreadyRunning = false,
    int IncomingCount = 0,
    int ConfirmationCount = 0,
    int InboxCount = 0,
    string? ErrorMessage = null)
{
    public static implicit operator bool(SyncCycleResult r) => r.Success;
}

public sealed class RouterWorker(
    AddressChangeRoutingService routingService,
    IEmailReader emailReader,
    IPersonRequestRepository repository,
    ICsvReportWriter reportWriter,
    RouterOptions options,
    ILogger<RouterWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim cycleGuard = new(1, 1);

    public RouterOptions Options => options;

    /// <summary>No automatic polling — sync only runs when the operator presses "Sincronizar
    /// ahora" on the dashboard (see IndexModel.OnPostSyncNowAsync), which calls RunCycleAsync
    /// directly. The schema is created once at startup in Program.cs.</summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    /// <summary>Runs one poll cycle. Returns false only when a cycle was already running and this call was skipped
    /// (used by the dashboard's manual "sync now" action to report accurate feedback).</summary>
    internal async Task<SyncCycleResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        if (!await cycleGuard.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            logger.LogWarning("Ciclo anterior aún en ejecución, se omite este tick");
            return new SyncCycleResult(Success: false, AlreadyRunning: true, ErrorMessage: "Ya hay una sincronización en curso, intente en unos segundos.");
        }

        try
        {
            var contacts = routingService.LoadDirectory();
            if (contacts.Count == 0)
            {
                logger.LogCritical(
                    "El directorio de comunas ({CsvPath}) está vacío o no se pudo leer. Se omite este ciclo completo para no perder correos silenciosamente",
                    options.ComunaDirectoryCsvPath);
                return new SyncCycleResult(Success: true, AlreadyRunning: false, ErrorMessage: $"El directorio de comunas ({options.ComunaDirectoryCsvPath}) está vacío o no se pudo leer.");
            }

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

            var bounceSince = DateTimeOffset.UtcNow.AddDays(-options.BounceLookbackDays);
            var inboxMessages = await emailReader.GetInboxMessagesSinceAsync(bounceSince, cancellationToken);
            foreach (var email in inboxMessages)
            {
                try
                {
                    routingService.ProcessPotentialBounce(email, contacts);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error procesando un posible rebote de la bandeja de entrada, se continúa con el resto del lote");
                }
            }

            reportWriter.Write(repository.GetAll(), options.ReportCsvPath);
            logger.LogInformation(
                "Ciclo completado: {IncomingCount} en '{SourceFolder}', {ConfirmationCount} en '{ConfirmationFolder}', {InboxCount} en bandeja de entrada",
                incoming.Count, options.SourceFolderName, confirmations.Count, options.ConfirmationFolderName, inboxMessages.Count);
            return new SyncCycleResult(Success: true, AlreadyRunning: false, IncomingCount: incoming.Count, ConfirmationCount: confirmations.Count, InboxCount: inboxMessages.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el ciclo de sondeo, se reintentará en el próximo tick");
            return new SyncCycleResult(Success: false, AlreadyRunning: false, ErrorMessage: $"Error al sincronizar con el servidor de correo: {ex.Message}");
        }
        finally
        {
            cycleGuard.Release();
        }
    }
}
