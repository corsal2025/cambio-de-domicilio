using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class PersonRequestCompletionTests
{
    [Fact]
    public void IsActionCompleted_UntouchedPendingCase_IsFalse()
    {
        var request = NewRequest();
        request.Status = RequestStatus.Pending;

        Assert.False(request.IsActionCompleted);
    }

    [Theory]
    [InlineData(RequestStatus.Uploaded)]
    [InlineData(RequestStatus.Confirmed)]
    public void IsActionCompleted_UploadedOrConfirmed_IsTrue(RequestStatus status)
    {
        var request = NewRequest();
        request.Status = status;

        Assert.True(request.IsActionCompleted);
    }

    [Fact]
    public void IsActionCompleted_SentToCaja_IsTrue()
    {
        var request = NewRequest();
        request.Status = RequestStatus.Pending;
        request.Destination = CaseDestination.Caja;

        Assert.True(request.IsActionCompleted);
    }

    [Fact]
    public void IsActionCompleted_ClosedWithoutFolder_IsTrue()
    {
        var request = NewRequest();
        request.Status = RequestStatus.Pending;
        request.ClosedWithoutFolderAt = DateTimeOffset.UtcNow;

        Assert.True(request.IsActionCompleted);
    }

    private static PersonRequest NewRequest() => new()
    {
        SourceMessageId = "msg-1",
        SourceSubject = "Cambio de domicilio",
        SourceSender = "comuna@example.cl",
    };
}
