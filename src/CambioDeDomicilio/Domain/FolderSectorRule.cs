namespace CambioDeDomicilio.Domain;

/// <summary>Single source of truth for where a physical folder lives, derived from the date of its
/// last folder. Screens, JSON responses and printed documents must all go through here.</summary>
public static class FolderSectorRule
{
    /// <summary>First day stored in Oficina 43; anything earlier is in Archivo.</summary>
    public static readonly DateOnly Oficina43Since = new(2023, 7, 1);

    public static FolderSector For(DateOnly fechaUltimaCarpeta) =>
        fechaUltimaCarpeta < Oficina43Since ? FolderSector.Archivo : FolderSector.Oficina43;
}
