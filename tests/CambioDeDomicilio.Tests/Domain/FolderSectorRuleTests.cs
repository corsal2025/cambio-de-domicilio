using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class FolderSectorRuleTests
{
    [Theory]
    [InlineData(2023, 6, 30, FolderSector.Archivo)]
    [InlineData(2023, 7, 1, FolderSector.Oficina43)]
    [InlineData(2024, 3, 15, FolderSector.Oficina43)]
    [InlineData(2015, 1, 1, FolderSector.Archivo)]
    public void For_UsesJulyFirst2023AsCutoff(int year, int month, int day, FolderSector expected)
    {
        Assert.Equal(expected, FolderSectorRule.For(new DateOnly(year, month, day)));
    }

    [Fact]
    public void PersonRequestSector_MatchesRule()
    {
        var request = new PersonRequest { SourceMessageId = "m", SourceSubject = "s", SourceSender = "a@b.cl", FechaUltimaCarpeta = new DateOnly(2023, 6, 30) };

        Assert.Equal(FolderSectorRule.For(new DateOnly(2023, 6, 30)), request.Sector);
    }
}
