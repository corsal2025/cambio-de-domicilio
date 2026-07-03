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

    [Fact]
    public void UpdateContactEmail_ValidChange_PersistsAndKeepsOtherRows()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path,
            "Comuna,ContactEmail,Domain\nCatemu,viejo@municatemu.cl,municatemu.cl\nColina,luis@colina.cl,colina.cl\n");
        var directory = new ComunaDirectory();

        var updated = directory.UpdateContactEmail(path, "Catemu", "nuevo@municatemu.cl");

        Assert.True(updated);
        var reloaded = directory.LoadFromCsv(path);
        Assert.Equal("nuevo@municatemu.cl", reloaded.Single(c => c.Comuna == "Catemu").ContactEmail);
        Assert.Equal("luis@colina.cl", reloaded.Single(c => c.Comuna == "Colina").ContactEmail);
        File.Delete(path);
    }

    [Fact]
    public void UpdateContactEmail_UnknownComuna_ReturnsFalseWithoutModifying()
    {
        var path = Path.GetTempFileName();
        var original = "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n";
        File.WriteAllText(path, original);
        var directory = new ComunaDirectory();

        var updated = directory.UpdateContactEmail(path, "NoExiste", "x@y.cl");

        Assert.False(updated);
        Assert.Equal(original, File.ReadAllText(path));
        File.Delete(path);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("dos@arrobas@x.cl")]
    [InlineData("con espacios@x.cl")]
    [InlineData("con,coma@x.cl")]
    [InlineData("sinpunto@dominio")]
    public void UpdateContactEmail_InvalidEmailShape_IsRejected(string invalidEmail)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");
        var directory = new ComunaDirectory();

        Assert.False(directory.UpdateContactEmail(path, "Catemu", invalidEmail));
        File.Delete(path);
    }
}
