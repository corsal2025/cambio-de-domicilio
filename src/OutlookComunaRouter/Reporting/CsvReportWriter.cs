using System.Text;
using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Reporting;

public interface ICsvReportWriter
{
    void Write(IReadOnlyList<PersonRequest> requests, string outputPath);
}

public sealed class CsvReportWriter : ICsvReportWriter
{
    public void Write(IReadOnlyList<PersonRequest> requests, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("full_name,rut,comuna,status,fecha_ultima_carpeta,sector,confirmed_at,Requiere revisión");

        foreach (var request in requests)
        {
            var requiresReview = request.NeedsReview ? "Sí" : "No";
            var sector = request.Sector switch
            {
                FolderSector.Archivo => "Archivo",
                FolderSector.Oficina43 => "Oficina 43",
                _ => string.Empty
            };
            builder.AppendLine(string.Join(',',
                Escape(request.FullName),
                Escape(request.Rut),
                Escape(request.Comuna),
                Escape(request.Status.ToString()),
                Escape(request.FechaUltimaCarpeta?.ToString("yyyy-MM-dd")),
                Escape(sector),
                Escape(request.ConfirmedAt?.ToString("yyyy-MM-dd HH:mm")),
                Escape(requiresReview)));
        }

        // Write to a temp file then move, so a reader never sees a half-written report.
        var tempPath = outputPath + ".tmp";
        File.WriteAllText(tempPath, builder.ToString(), Encoding.UTF8);
        File.Move(tempPath, outputPath, overwrite: true);
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
