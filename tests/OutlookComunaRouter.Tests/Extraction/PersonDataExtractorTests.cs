using OutlookComunaRouter.Extraction;
using Xunit;

namespace OutlookComunaRouter.Tests.Extraction;

public class PersonDataExtractorTests
{
    [Fact]
    public void Extract_UppercaseNameDottedRut_ExtractsBoth()
    {
        var body = "Se notifica cambio de domicilio de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7 hacia Valparaíso.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", result.FullName);
        Assert.Equal("18.785.387-7", result.Rut);
    }

    [Fact]
    public void Extract_MixedCaseNameUndottedRut_NormalizesRut()
    {
        var body = "Contribuyente: Gustavo Andrés Peña Castro RUT: 18785387-7";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("Gustavo Andrés Peña Castro", result.FullName);
        Assert.Equal("18.785.387-7", result.Rut);
    }

    [Fact]
    public void Extract_NoRecognizableRut_ReturnsNullRut()
    {
        var body = "Este correo no contiene un identificador válido.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Null(result.Rut);
    }

    [Fact]
    public void Extract_InvalidCheckDigit_ReturnsNullRut()
    {
        var body = "RUT: 18.785.387-6"; // wrong check digit

        var result = PersonDataExtractor.Extract(body);

        Assert.Null(result.Rut);
    }
}
