namespace CambioDeDomicilio.Domain;

/// <summary>Human-readable sector names for screens and printed documents. The enum member
/// <see cref="FolderSector.Oficina43"/> can't contain a space, so UI code must never print it
/// with ToString().</summary>
public static class FolderSectorDisplay
{
    public static string ToDisplayName(this FolderSector sector) => sector switch
    {
        FolderSector.Archivo => "Archivo",
        FolderSector.Oficina43 => "Oficina 43",
        _ => sector.ToString()
    };

    /// <summary>"—" when the sector isn't derivable yet (no última-carpeta date).</summary>
    public static string ToDisplayName(this FolderSector? sector) =>
        sector is { } value ? value.ToDisplayName() : "—";
}
