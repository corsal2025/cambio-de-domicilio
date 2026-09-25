using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Ews;
using Xunit;

namespace CambioDeDomicilio.Tests.Ews;

public class EwsEmailReaderTests
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string TNs = "http://schemas.microsoft.com/exchange/services/2006/types";
    private const string MNs = "http://schemas.microsoft.com/exchange/services/2006/messages";

    private static readonly string FindFolderFoundXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindFolderResponse>
              <m:ResponseMessages>
                <m:FindFolderResponseMessage ResponseClass="Success">
                  <m:RootFolder TotalItemsInView="1">
                    <t:Folders>
                      <t:Folder><t:FolderId Id="folder-abc" ChangeKey="ck-1"/></t:Folder>
                    </t:Folders>
                  </m:RootFolder>
                </m:FindFolderResponseMessage>
              </m:ResponseMessages>
            </m:FindFolderResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static readonly string FindFolderNotFoundXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindFolderResponse>
              <m:ResponseMessages>
                <m:FindFolderResponseMessage ResponseClass="Success">
                  <m:RootFolder TotalItemsInView="0"><t:Folders/></m:RootFolder>
                </m:FindFolderResponseMessage>
              </m:ResponseMessages>
            </m:FindFolderResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static readonly string EmptyFindItemXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindItemResponse>
              <m:ResponseMessages>
                <m:FindItemResponseMessage ResponseClass="Success">
                  <m:RootFolder TotalItemsInView="0"><t:Items/></m:RootFolder>
                </m:FindItemResponseMessage>
              </m:ResponseMessages>
            </m:FindItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    [Fact]
    public async Task GetMessagesInFolderAsync_ResolvesFolderOnceAndReusesCache()
    {
        var client = new RecordingClient([FindFolderFoundXml, EmptyFindItemXml, EmptyFindItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);
        await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);

        var findFolderCalls = client.Requests.Count(r => r.Contains("FindFolder"));
        Assert.Equal(1, findFolderCalls); // resolved once, cached for the second call
    }

    [Fact]
    public async Task GetMessagesInFolderAsync_DifferentFolderNames_ResolvedIndependently()
    {
        var client = new RecordingClient([FindFolderFoundXml, EmptyFindItemXml, FindFolderFoundXml, EmptyFindItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);
        await reader.GetMessagesInFolderAsync("CARP. YA SUBIDAS", CancellationToken.None);

        var findFolderCalls = client.Requests.Count(r => r.Contains("FindFolder"));
        Assert.Equal(2, findFolderCalls); // each distinct folder name resolved once
    }

    [Fact]
    public async Task GetInboxMessagesSinceAsync_QueriesInboxDirectlyWithoutResolvingAFolderByName()
    {
        var client = new RecordingClient([EmptyFindItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var result = await reader.GetInboxMessagesSinceAsync(DateTimeOffset.UtcNow.AddDays(-14), CancellationToken.None);

        Assert.Empty(result);
        Assert.DoesNotContain(client.Requests, r => r.Contains("FindFolder"));
        Assert.Contains(client.Requests, r => r.Contains("FindItem") && r.Contains("\"inbox\""));
    }

    [Fact]
    public async Task GetMessagesInFolderAsync_FolderNotFound_ReturnsEmptyWithoutCallingFindItem()
    {
        var client = new RecordingClient([FindFolderNotFoundXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var result = await reader.GetMessagesInFolderAsync("Carpeta Inexistente", CancellationToken.None);

        Assert.Empty(result);
        Assert.DoesNotContain(client.Requests, r => r.Contains("FindItem"));
    }

    private static readonly string FindItemErrorXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindItemResponse>
              <m:ResponseMessages>
                <m:FindItemResponseMessage ResponseClass="Error">
                  <m:ResponseCode>ErrorFolderNotFound</m:ResponseCode>
                </m:FindItemResponseMessage>
              </m:ResponseMessages>
            </m:FindItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static string FindItemWithIdsXml(int count) => $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindItemResponse>
              <m:ResponseMessages>
                <m:FindItemResponseMessage ResponseClass="Success">
                  <m:RootFolder TotalItemsInView="{count}">
                    <t:Items>
                    {string.Join('\n', Enumerable.Range(1, count).Select(i => $"""<t:Message><t:ItemId Id="item-{i}" ChangeKey="ck-{i}"/></t:Message>"""))}
                    </t:Items>
                  </m:RootFolder>
                </m:FindItemResponseMessage>
              </m:ResponseMessages>
            </m:FindItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static readonly string EmptyGetItemXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:GetItemResponse>
              <m:ResponseMessages>
                <m:GetItemResponseMessage ResponseClass="Success">
                  <m:Items/>
                </m:GetItemResponseMessage>
              </m:ResponseMessages>
            </m:GetItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    [Fact]
    public async Task GetMessagesInFolderAsync_CachedFolderIdGoesStale_ReResolvesAndRetries()
    {
        // Cycle 1 resolves and caches the folder. Cycle 2 hits a stale-folder EWS error,
        // must drop the cache entry, re-resolve, and retry instead of failing forever.
        var client = new RecordingClient([
            FindFolderFoundXml, EmptyFindItemXml,   // cycle 1: resolve + list OK
            FindItemErrorXml,                        // cycle 2: cached id now stale
            FindFolderFoundXml, EmptyFindItemXml]);  // cycle 2: re-resolve + retry OK
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);
        var result = await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(2, client.Requests.Count(r => r.Contains("FindFolder")));
    }

    [Fact]
    public async Task GetMessagesInFolderAsync_ManyItems_FetchesDetailsInChunks()
    {
        var client = new RecordingClient([
            FindFolderFoundXml,
            FindItemWithIdsXml(120),
            EmptyGetItemXml, EmptyGetItemXml, EmptyGetItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);

        var getItemCalls = client.Requests.Where(r => r.Contains("GetItem")).ToList();
        Assert.Equal(3, getItemCalls.Count); // 120 ids in chunks of 50 -> 50+50+20
        Assert.All(getItemCalls, r => Assert.True(CountOccurrences(r, "<t:ItemId ") <= 50));
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static readonly string FindItemOneMatchXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:FindItemResponse>
              <m:ResponseMessages>
                <m:FindItemResponseMessage ResponseClass="Success">
                  <m:RootFolder TotalItemsInView="1">
                    <t:Items>
                      <t:Message><t:ItemId Id="src-item-1" ChangeKey="src-ck-1"/></t:Message>
                    </t:Items>
                  </m:RootFolder>
                </m:FindItemResponseMessage>
              </m:ResponseMessages>
            </m:FindItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static readonly string MoveItemSuccessXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:MoveItemResponse>
              <m:ResponseMessages>
                <m:MoveItemResponseMessage ResponseClass="Success">
                  <m:Items>
                    <t:Message><t:ItemId Id="dest-item-1" ChangeKey="dest-ck-1"/></t:Message>
                  </m:Items>
                </m:MoveItemResponseMessage>
              </m:ResponseMessages>
            </m:MoveItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private static readonly string UpdateItemSuccessXml = $"""
        <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
          <soap:Body>
            <m:UpdateItemResponse>
              <m:ResponseMessages>
                <m:UpdateItemResponseMessage ResponseClass="Success" />
              </m:ResponseMessages>
            </m:UpdateItemResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    [Fact]
    public async Task MoveAndMarkUnreadAsync_MatchFound_MovesAndMarksUnread()
    {
        var client = new RecordingClient([
            FindFolderFoundXml, // resolve source folder
            FindFolderFoundXml, // resolve destination folder
            FindItemOneMatchXml, // find the item by InternetMessageId
            MoveItemSuccessXml, // move it
            UpdateItemSuccessXml]); // mark unread at destination
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var moved = await reader.MoveAndMarkUnreadAsync("<abc@munivalpo.cl>", "CARP. PARA PEDIR", "CARP. YA SUBIDAS", CancellationToken.None);

        Assert.True(moved);
        Assert.Contains(client.Requests, r => r.Contains("MoveItem") && r.Contains("src-item-1"));
        Assert.Contains(client.Requests, r => r.Contains("UpdateItem") && r.Contains("dest-item-1"));
    }

    [Fact]
    public async Task MoveAndMarkUnreadAsync_NoMatchingItem_ReturnsFalseWithoutMoving()
    {
        var client = new RecordingClient([FindFolderFoundXml, FindFolderFoundXml, EmptyFindItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var moved = await reader.MoveAndMarkUnreadAsync("<no-existe@munivalpo.cl>", "CARP. PARA PEDIR", "CARP. YA SUBIDAS", CancellationToken.None);

        Assert.False(moved);
        Assert.DoesNotContain(client.Requests, r => r.Contains("MoveItem"));
    }

    [Fact]
    public async Task MoveAndMarkUnreadAsync_SourceFolderNotFound_ReturnsFalse()
    {
        var client = new RecordingClient([FindFolderNotFoundXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var moved = await reader.MoveAndMarkUnreadAsync("<abc@munivalpo.cl>", "Carpeta Inexistente", "CARP. YA SUBIDAS", CancellationToken.None);

        Assert.False(moved);
    }

    [Fact]
    public async Task GetMessagesInFolderAsync_MultiplePages_FetchesAllPagesUntilLast()
    {
        var page1Xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:FindItemResponse>
                  <m:ResponseMessages>
                    <m:FindItemResponseMessage ResponseClass="Success">
                      <m:RootFolder TotalItemsInView="2" IncludesLastItemInRange="false">
                        <t:Items>
                          <t:Message><t:ItemId Id="item-p1-1" ChangeKey="ck-1"/></t:Message>
                          <t:Message><t:ItemId Id="item-p1-2" ChangeKey="ck-2"/></t:Message>
                        </t:Items>
                      </m:RootFolder>
                    </m:FindItemResponseMessage>
                  </m:ResponseMessages>
                </m:FindItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var page2Xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:FindItemResponse>
                  <m:ResponseMessages>
                    <m:FindItemResponseMessage ResponseClass="Success">
                      <m:RootFolder TotalItemsInView="1" IncludesLastItemInRange="true">
                        <t:Items>
                          <t:Message><t:ItemId Id="item-p2-1" ChangeKey="ck-3"/></t:Message>
                        </t:Items>
                      </m:RootFolder>
                    </m:FindItemResponseMessage>
                  </m:ResponseMessages>
                </m:FindItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var getItemXml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:GetItemResponse>
                  <m:ResponseMessages>
                    <m:GetItemResponseMessage ResponseClass="Success">
                      <m:Items>
                        <t:Message>
                          <t:ItemId Id="item-p1-1" ChangeKey="ck-1"/>
                          <t:Subject>Req 1</t:Subject>
                          <t:Body BodyType="Text">RUT 1-9</t:Body>
                          <t:DateTimeReceived>2026-09-01T10:00:00Z</t:DateTimeReceived>
                          <t:ConversationId Id="c-1"/>
                          <t:From><t:Mailbox><t:EmailAddress>a@b.cl</t:EmailAddress></t:Mailbox></t:From>
                          <t:InternetMessageId>&lt;m1@b.cl&gt;</t:InternetMessageId>
                        </t:Message>
                        <t:Message>
                          <t:ItemId Id="item-p1-2" ChangeKey="ck-2"/>
                          <t:Subject>Req 2</t:Subject>
                          <t:Body BodyType="Text">RUT 2-7</t:Body>
                          <t:DateTimeReceived>2026-09-02T10:00:00Z</t:DateTimeReceived>
                          <t:ConversationId Id="c-2"/>
                          <t:From><t:Mailbox><t:EmailAddress>a@b.cl</t:EmailAddress></t:Mailbox></t:From>
                          <t:InternetMessageId>&lt;m2@b.cl&gt;</t:InternetMessageId>
                        </t:Message>
                        <t:Message>
                          <t:ItemId Id="item-p2-1" ChangeKey="ck-3"/>
                          <t:Subject>Req 3</t:Subject>
                          <t:Body BodyType="Text">RUT 3-5</t:Body>
                          <t:DateTimeReceived>2026-09-03T10:00:00Z</t:DateTimeReceived>
                          <t:ConversationId Id="c-3"/>
                          <t:From><t:Mailbox><t:EmailAddress>a@b.cl</t:EmailAddress></t:Mailbox></t:From>
                          <t:InternetMessageId>&lt;m3@b.cl&gt;</t:InternetMessageId>
                        </t:Message>
                      </m:Items>
                    </m:GetItemResponseMessage>
                  </m:ResponseMessages>
                </m:GetItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var client = new RecordingClient([FindFolderFoundXml, page1Xml, page2Xml, getItemXml]);
        var reader = new EwsEmailReader(client, NullLogger<EwsEmailReader>.Instance);

        var messages = await reader.GetMessagesInFolderAsync("CARP. PARA PEDIR", CancellationToken.None);

        Assert.Equal(3, messages.Count);
        var findRequests = client.Requests.Where(r => r.Contains("FindItem")).ToList();
        Assert.Equal(2, findRequests.Count);
        Assert.Contains("Offset=\"0\"", findRequests[0]);
        Assert.Contains("Offset=\"2\"", findRequests[1]);
    }

    private sealed class RecordingClient(IReadOnlyList<string> responses) : IEwsClient
    {
        private int callIndex;
        public List<string> Requests { get; } = [];

        public Task<XDocument> SendAsync(string soapRequest, CancellationToken cancellationToken)
        {
            Requests.Add(soapRequest);
            var response = responses[Math.Min(callIndex, responses.Count - 1)];
            callIndex++;
            return Task.FromResult(XDocument.Parse(response));
        }
    }
}
