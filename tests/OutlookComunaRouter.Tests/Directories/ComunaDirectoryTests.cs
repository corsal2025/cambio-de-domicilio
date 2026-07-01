using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using Xunit;

namespace OutlookComunaRouter.Tests.Directories;

public class ComunaDirectoryTests
{
    private static readonly IReadOnlyList<ComunaContact> Contacts =
    [
        new ComunaContact("Catemu", "rfloresc@municatemu.cl", "municatemu.cl")
    ];

    [Fact]
    public void ResolveByDomain_RecognizedComunaDomain_ReturnsContact()
    {
        var directory = new ComunaDirectory();

        var result = directory.ResolveByDomain("municatemu.cl", "munivalpo.cl", Contacts);

        Assert.NotNull(result);
        Assert.Equal("Catemu", result!.Comuna);
    }

    [Fact]
    public void ResolveByDomain_OwnDomain_ReturnsNull()
    {
        var directory = new ComunaDirectory();

        var result = directory.ResolveByDomain("munivalpo.cl", "munivalpo.cl", Contacts);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveByDomain_UnknownDomain_ReturnsNull()
    {
        var directory = new ComunaDirectory();

        var result = directory.ResolveByDomain("otrodominio.cl", "munivalpo.cl", Contacts);

        Assert.Null(result);
    }

    [Fact]
    public void LoadFromCsv_ValidFile_ParsesRows()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");
        var directory = new ComunaDirectory();

        var result = directory.LoadFromCsv(path);

        Assert.Single(result);
        Assert.Equal("Catemu", result[0].Comuna);
        File.Delete(path);
    }

    [Fact]
    public void LoadFromCsv_MalformedRow_IsSkipped()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "Comuna,ContactEmail,Domain\nFilaInvalida\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");
        var directory = new ComunaDirectory();

        var result = directory.LoadFromCsv(path);

        Assert.Single(result);
        File.Delete(path);
    }

    [Fact]
    public void LoadFromCsv_MissingFile_ReturnsEmpty()
    {
        var directory = new ComunaDirectory();

        var result = directory.LoadFromCsv("no-existe.csv");

        Assert.Empty(result);
    }
}
