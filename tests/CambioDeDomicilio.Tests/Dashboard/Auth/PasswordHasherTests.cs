using CambioDeDomicilio.Dashboard.Auth;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Verify_RoundTrip_Succeeds()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");

        Assert.True(PasswordHasher.Verify("Cont2026#", hash, salt, iterations));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");

        Assert.False(PasswordHasher.Verify("otra-clave", hash, salt, iterations));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSalts()
    {
        var first = PasswordHasher.Hash("Cont2026#");
        var second = PasswordHasher.Hash("Cont2026#");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
