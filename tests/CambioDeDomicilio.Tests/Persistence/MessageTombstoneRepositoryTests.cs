using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

public class MessageTombstoneRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"tombstone-test-{Guid.NewGuid():N}.db");
    private readonly IMessageTombstoneRepository repository;

    public MessageTombstoneRepositoryTests()
    {
        TestDatabase.Migrate(dbPath);
        repository = new MessageTombstoneRepository($"Data Source={dbPath}");
    }

    [Fact]
    public void IsSourceMessageDeleted_UnknownMessage_ReturnsFalse()
    {
        Assert.False(repository.IsSourceMessageDeleted("msg-1"));
    }

    [Fact]
    public void RecordDeletedSourceMessage_IsQueryableAndIdempotent()
    {
        repository.RecordDeletedSourceMessage("msg-1");
        repository.RecordDeletedSourceMessage("msg-1"); // duplicate must not throw

        Assert.True(repository.IsSourceMessageDeleted("msg-1"));
        Assert.False(repository.IsSourceMessageDeleted("msg-2"));
    }

    [Fact]
    public void RecordProcessedBounce_IsIdempotentAndQueryable()
    {
        Assert.False(repository.IsBounceProcessed("ndr-1"));

        repository.RecordProcessedBounce("ndr-1");
        Assert.True(repository.IsBounceProcessed("ndr-1"));

        repository.RecordProcessedBounce("ndr-1"); // duplicate must not throw
        Assert.True(repository.IsBounceProcessed("ndr-1"));
    }

    [Fact]
    public void DeletedSourceMessagesAndProcessedBounces_AreIndependentNamespaces()
    {
        repository.RecordDeletedSourceMessage("same-id");

        Assert.False(repository.IsBounceProcessed("same-id"));
    }

    public void Dispose() => TestDatabase.Cleanup(dbPath);
}
