using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Reporting;
using Xunit;

namespace CambioDeDomicilio.Tests.Reporting;

public class CsvReportWriterTests : IDisposable
{
    private readonly string outputPath = Path.Combine(Path.GetTempPath(), $"report-test-{Guid.NewGuid():N}.csv");

    [Fact]
    public void Write_NeedsReviewRecord_MarksColumnSi()
    {
        var writer = new CsvReportWriter();
        var requests = new List<PersonRequest>
        {
            new()
            {
                FullName = null,
                Rut = null,
                Comuna = null,
                SourceMessageId = "msg-1",
                SourceSubject = "Cambio de domicilio",
                SourceSender = "rfloresc@municatemu.cl",
                NeedsReview = true,
                Status = RequestStatus.Pending
            }
        };

        writer.Write(requests, outputPath);
        var content = File.ReadAllText(outputPath);

        Assert.Contains("Sí", content);
    }

    [Fact]
    public void Write_ConfirmedRecord_IncludesFechaSectorAndConfirmedAt()
    {
        var writer = new CsvReportWriter();
        var requests = new List<PersonRequest>
        {
            new()
            {
                FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
                Rut = "18.785.387-7",
                Comuna = "Catemu",
                SourceMessageId = "msg-1",
                SourceSubject = "Solicitud de carpeta",
                SourceSender = "rfloresc@municatemu.cl",
                NeedsReview = false,
                Status = RequestStatus.Confirmed,
                FechaUltimaCarpeta = new DateOnly(2022, 3, 15),
                ConfirmedAt = new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero)
            }
        };

        writer.Write(requests, outputPath);
        var lines = File.ReadAllLines(outputPath);

        Assert.Equal(2, lines.Length); // header + 1 row
        Assert.Contains("2022-03-15", lines[1]);
        Assert.Contains("Archivo", lines[1]); // fecha anterior a julio 2023 → Archivo
        Assert.Contains("2026-06-01 10:30", lines[1]);
        Assert.EndsWith(",No", lines[1]);
    }

    [Fact]
    public void Write_FechaAfterJuly2023_SectorIsOficina43()
    {
        var writer = new CsvReportWriter();
        var requests = new List<PersonRequest>
        {
            new()
            {
                FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
                Rut = "18.785.387-7",
                Comuna = "Catemu",
                SourceMessageId = "msg-1",
                SourceSubject = "Solicitud de carpeta",
                SourceSender = "rfloresc@municatemu.cl",
                NeedsReview = false,
                Status = RequestStatus.Pending,
                FechaUltimaCarpeta = new DateOnly(2024, 1, 10)
            }
        };

        writer.Write(requests, outputPath);
        var lines = File.ReadAllLines(outputPath);

        Assert.Contains("Oficina 43", lines[1]);
    }

    public void Dispose()
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }
}
