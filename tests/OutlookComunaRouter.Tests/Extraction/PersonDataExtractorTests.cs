using OutlookComunaRouter.Extraction;
using Xunit;

namespace OutlookComunaRouter.Tests.Extraction;

public class PersonDataExtractorTests
{
    [Fact]
    public void Extract_PrefixedRut_ExtractsAdjacentName()
    {
        var body = "Se solicita la carpeta de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7 por cambio de domicilio.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", result.FullName);
        Assert.Equal("18.785.387-7", result.Rut);
    }

    [Fact]
    public void Extract_RunPrefixVariant_IsRecognized()
    {
        var body = "carpeta de don Gustavo Andrés Peña Castro RUN 18785387-7, gracias.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("Gustavo Andrés Peña Castro", result.FullName);
        Assert.Equal("18.785.387-7", result.Rut);
    }

    [Fact]
    public void Extract_BareDottedRutWithoutPrefix_IsRecognized()
    {
        var body = "Junto con saludar, solicito la carpeta de GUSTAVO ANDRÉS PEÑA CASTRO 18.785.387-7 para tramitación.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", result.FullName);
        Assert.Equal("18.785.387-7", result.Rut);
    }

    [Fact]
    public void Extract_BareUndottedRut_IsRecognizedAndNormalized()
    {
        var body = "solicita carpeta Gustavo Andrés Peña Castro 18785387-7";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("18.785.387-7", result.Rut);
        Assert.Equal("Gustavo Andrés Peña Castro", result.FullName);
    }

    [Fact]
    public void Extract_ExternalBanner_IsNeverExtractedAsName()
    {
        var body = "CORREO EXTERNO : No haga clic en ningún enlace ni abra ningún archivo adjunto " +
                   "a menos que confíe en el remitente y sepa que el contenido es seguro. " +
                   "Solicito carpeta de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", result.FullName);
        Assert.DoesNotContain("EXTERNO", result.FullName);
    }

    [Fact]
    public void Extract_NameAfterRut_IsFoundAsFallback()
    {
        var body = "Solicito carpeta del contribuyente con RUT: 18.785.387-7 GUSTAVO ANDRÉS PEÑA CASTRO.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", result.FullName);
    }

    [Fact]
    public void Extract_HonorificsStrippedFromName()
    {
        var body = "carpeta de Don Gustavo Peña RUT 18.785.387-7";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("Gustavo Peña", result.FullName);
    }

    [Fact]
    public void Extract_SpaceSeparatedCheckDigit_VinaFormat_IsRecognized()
    {
        // Verbatim shape of Viña del Mar's system output: undotted body, check digit after a
        // run of spaces, name AFTER the RUT padded with space runs and CRLFs, then filler text.
        var body = "solicitar los antecedentes correspondientes a:\r\n\r\n18785387        7       CARVAJAL        LUCERO  MATIAS JORGE\r\n\r\nQuien posee una licencia de conducir emitida en ese municipio.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("18.785.387-7", result.Rut);
        Assert.Equal("CARVAJAL LUCERO MATIAS JORGE", result.FullName);
    }

    [Fact]
    public void Extract_NoRut_ReturnsBothNull()
    {
        var body = "CORREO EXTERNO : No haga clic. Estimados, adjunto la solicitud en el archivo. Saludos Cordiales.";

        var result = PersonDataExtractor.Extract(body);

        Assert.Null(result.Rut);
        Assert.Null(result.FullName); // a name without an anchoring RUT is not trustworthy
    }

    [Fact]
    public void Extract_InvalidCheckDigit_IsRejected()
    {
        var body = "GUSTAVO PEÑA RUT: 18.785.387-6"; // wrong check digit

        var result = PersonDataExtractor.Extract(body);

        Assert.Null(result.Rut);
    }

    [Fact]
    public void Extract_InvalidFirstRutButValidSecond_UsesTheValidOne()
    {
        var body = "ref 11.111.111-0 y carpeta de GUSTAVO PEÑA CASTRO RUT 18.785.387-7";

        var result = PersonDataExtractor.Extract(body);

        Assert.Equal("18.785.387-7", result.Rut);
        Assert.Equal("GUSTAVO PEÑA CASTRO", result.FullName);
    }
}
