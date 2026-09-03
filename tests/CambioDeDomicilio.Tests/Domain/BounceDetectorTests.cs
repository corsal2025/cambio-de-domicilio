using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class BounceDetectorTests
{
    private static IncomingEmail Email(string sender, string subject, string body) =>
        new("ndr-1", "conv", subject, sender, body, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("postmaster@munivalpo.cl")]
    [InlineData("MAILER-DAEMON@mx273.antispamcloud.com")]
    [InlineData("")]
    [InlineData("<>")]
    public void LooksLikeConfirmationBounce_NdrSenderAndConasetBody_IsTrue(string sender)
    {
        var email = Email(
            sender,
            "Undeliverable: Carpeta subida a Conaset - JUAN PEREZ, RUT 12.345.678-5",
            "Your message to vmeirelles@mph.cl could not be delivered.\n" +
            "550 The sending IP is listed as a source of phishing.\n\n" +
            "Original message:\nSe informa que la carpeta ya fue subida al sistema de Conaset.");

        Assert.True(BounceDetector.LooksLikeConfirmationBounce(email));
    }

    [Fact]
    public void LooksLikeConfirmationBounce_SpanishNdrSubject_IsTrue()
    {
        var email = Email(
            "correo@otracomuna.cl",
            "Correo no entregado: Carpeta subida a Conaset",
            "El mensaje no pudo ser entregado. Sistema de Conaset.");

        Assert.True(BounceDetector.LooksLikeConfirmationBounce(email));
    }

    [Fact]
    public void LooksLikeConfirmationBounce_NdrSenderButNoConasetMention_IsFalse()
    {
        // A bounce for some other email that happens to hit this mailbox — not ours to act on.
        var email = Email(
            "postmaster@munivalpo.cl",
            "Undeliverable: Reunión de coordinación",
            "Your message could not be delivered to the recipient.");

        Assert.False(BounceDetector.LooksLikeConfirmationBounce(email));
    }

    [Fact]
    public void LooksLikeConfirmationBounce_NormalComunaReplyMentioningConaset_IsFalse()
    {
        // A real reply from a comuna is not an NDR even though it mentions Conaset.
        var email = Email(
            "rfloresc@municatemu.cl",
            "RE: Carpeta subida a Conaset - JUAN PEREZ",
            "Gracias, recibido conforme. Saludos.");

        Assert.False(BounceDetector.LooksLikeConfirmationBounce(email));
    }
}
