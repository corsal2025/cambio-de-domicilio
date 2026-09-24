using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class FolderSectorDisplayTests
{
    [Theory]
    [InlineData(FolderSector.Archivo, "Archivo")]
    [InlineData(FolderSector.Oficina43, "Oficina 43")]
    public void ToDisplayName_ReturnsHumanReadableName(FolderSector sector, string expected)
    {
        Assert.Equal(expected, sector.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_NullSector_ReturnsDash()
    {
        FolderSector? sector = null;

        Assert.Equal("—", sector.ToDisplayName());
    }
}
