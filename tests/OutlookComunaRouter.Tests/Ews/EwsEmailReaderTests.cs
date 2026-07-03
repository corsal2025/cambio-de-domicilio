using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using OutlookComunaRouter.Ews;
using Xunit;

namespace OutlookComunaRouter.Tests.Ews;

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
        await reader.GetMessagesInFolderAsync("CARP. YA PEDIDAS", CancellationToken.None);

        var findFolderCalls = client.Requests.Count(r => r.Contains("FindFolder"));
        Assert.Equal(2, findFolderCalls); // each distinct folder name resolved once
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
