using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Reporting;
using Xunit;

namespace OutlookComunaRouter.Tests.Reporting;

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
    public void Write_RespondedRecord_IncludesLastFolderDateAndNoColumnFlag()
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
                SourceSubject = "Cambio de domicilio",
                SourceSender = "rfloresc@municatemu.cl",
                NeedsReview = false,
                Status = RequestStatus.Responded,
                LastFolderDate = "2026-06-01"
            }
        };

        writer.Write(requests, outputPath);
        var lines = File.ReadAllLines(outputPath);

        Assert.Equal(2, lines.Length); // header + 1 row
        Assert.Contains("2026-06-01", lines[1]);
        Assert.EndsWith(",No", lines[1]);
    }

    public void Dispose()
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }
}
