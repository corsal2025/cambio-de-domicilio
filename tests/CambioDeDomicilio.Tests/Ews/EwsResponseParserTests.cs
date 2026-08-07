using System.Xml.Linq;
using CambioDeDomicilio.Ews;
using Xunit;

namespace CambioDeDomicilio.Tests.Ews;

public class EwsResponseParserTests
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string TNs = "http://schemas.microsoft.com/exchange/services/2006/types";
    private const string MNs = "http://schemas.microsoft.com/exchange/services/2006/messages";

    [Fact]
    public void ParseFindItemResponse_ReturnsItemRefs()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:FindItemResponse>
                  <m:ResponseMessages>
                    <m:FindItemResponseMessage ResponseClass="Success">
                      <m:RootFolder TotalItemsInView="2">
                        <t:Items>
                          <t:Message><t:ItemId Id="AAMk1" ChangeKey="CQAAAB1"/></t:Message>
                          <t:Message><t:ItemId Id="AAMk2" ChangeKey="CQAAAB2"/></t:Message>
                        </t:Items>
                      </m:RootFolder>
                    </m:FindItemResponseMessage>
                  </m:ResponseMessages>
                </m:FindItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var refs = EwsResponseParser.ParseFindItemResponse(XDocument.Parse(xml));

        Assert.Equal(2, refs.Count);
        Assert.Equal("AAMk1", refs[0].Id);
        Assert.Equal("CQAAAB2", refs[1].ChangeKey);
    }

    [Fact]
    public void ParseGetItemResponse_MapsAllFields()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:GetItemResponse>
                  <m:ResponseMessages>
                    <m:GetItemResponseMessage ResponseClass="Success">
                      <m:Items>
                        <t:Message>
                          <t:ItemId Id="AAMk1" ChangeKey="CQ1"/>
                          <t:Subject>Cambio de domicilio</t:Subject>
                          <t:Body BodyType="Text">GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7</t:Body>
                          <t:DateTimeReceived>2026-07-02T10:00:00Z</t:DateTimeReceived>
                          <t:ConversationId Id="Conv1"/>
                          <t:From><t:Mailbox><t:EmailAddress>rfloresc@municatemu.cl</t:EmailAddress></t:Mailbox></t:From>
                          <t:InternetMessageId>&lt;abc123@municatemu.cl&gt;</t:InternetMessageId>
                        </t:Message>
                      </m:Items>
                    </m:GetItemResponseMessage>
                  </m:ResponseMessages>
                </m:GetItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var emails = EwsResponseParser.ParseGetItemResponse(XDocument.Parse(xml));

        var email = Assert.Single(emails);
        Assert.Equal("<abc123@municatemu.cl>", email.MessageId);
        Assert.Equal("Conv1", email.ConversationId);
        Assert.Equal("rfloresc@municatemu.cl", email.SenderAddress);
        Assert.Contains("18.785.387-7", email.BodyText);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero), email.ReceivedAt);
    }

    [Fact]
    public void ParseGetItemResponse_MessageWithoutInternetMessageId_IsSkipped()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:GetItemResponse>
                  <m:ResponseMessages>
                    <m:GetItemResponseMessage ResponseClass="Success">
                      <m:Items>
                        <t:Message>
                          <t:Subject>Sin id</t:Subject>
                          <t:From><t:Mailbox><t:EmailAddress>x@y.cl</t:EmailAddress></t:Mailbox></t:From>
                        </t:Message>
                      </m:Items>
                    </m:GetItemResponseMessage>
                  </m:ResponseMessages>
                </m:GetItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        Assert.Empty(EwsResponseParser.ParseGetItemResponse(XDocument.Parse(xml)));
    }

    [Fact]
    public void ParseFindFolderResponse_MatchFound_ReturnsFolderRef()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:FindFolderResponse>
                  <m:ResponseMessages>
                    <m:FindFolderResponseMessage ResponseClass="Success">
                      <m:RootFolder TotalItemsInView="1">
                        <t:Folders>
                          <t:Folder>
                            <t:FolderId Id="folder-abc" ChangeKey="ck-1"/>
                            <t:DisplayName>Para pedir</t:DisplayName>
                          </t:Folder>
                        </t:Folders>
                      </m:RootFolder>
                    </m:FindFolderResponseMessage>
                  </m:ResponseMessages>
                </m:FindFolderResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var folder = EwsResponseParser.ParseFindFolderResponse(XDocument.Parse(xml));

        Assert.NotNull(folder);
        Assert.Equal("folder-abc", folder!.Id);
        Assert.Equal("ck-1", folder.ChangeKey);
    }

    [Fact]
    public void ParseFindFolderResponse_NoMatch_ReturnsNull()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:FindFolderResponse>
                  <m:ResponseMessages>
                    <m:FindFolderResponseMessage ResponseClass="Success">
                      <m:RootFolder TotalItemsInView="0">
                        <t:Folders/>
                      </m:RootFolder>
                    </m:FindFolderResponseMessage>
                  </m:ResponseMessages>
                </m:FindFolderResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        Assert.Null(EwsResponseParser.ParseFindFolderResponse(XDocument.Parse(xml)));
    }

    [Fact]
    public void ParseMoveItemResponse_ReturnsNewItemIdAtDestination()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:MoveItemResponse>
                  <m:ResponseMessages>
                    <m:MoveItemResponseMessage ResponseClass="Success">
                      <m:Items>
                        <t:Message><t:ItemId Id="NewId1" ChangeKey="NewCk1"/></t:Message>
                      </m:Items>
                    </m:MoveItemResponseMessage>
                  </m:ResponseMessages>
                </m:MoveItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var moved = EwsResponseParser.ParseMoveItemResponse(XDocument.Parse(xml));

        Assert.NotNull(moved);
        Assert.Equal("NewId1", moved!.Id);
        Assert.Equal("NewCk1", moved.ChangeKey);
    }

    [Fact]
    public void ParseMoveItemResponse_ErrorResponse_Throws()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:MoveItemResponse>
                  <m:ResponseMessages>
                    <m:MoveItemResponseMessage ResponseClass="Error">
                      <m:ResponseCode>ErrorItemNotFound</m:ResponseCode>
                    </m:MoveItemResponseMessage>
                  </m:ResponseMessages>
                </m:MoveItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        Assert.Throws<InvalidOperationException>(() => EwsResponseParser.ParseMoveItemResponse(XDocument.Parse(xml)));
    }

    [Fact]
    public void EnsureSuccess_ErrorResponse_Throws()
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="{SoapNs}" xmlns:t="{TNs}" xmlns:m="{MNs}">
              <soap:Body>
                <m:CreateItemResponse>
                  <m:ResponseMessages>
                    <m:CreateItemResponseMessage ResponseClass="Error">
                      <m:ResponseCode>ErrorSendAsDenied</m:ResponseCode>
                    </m:CreateItemResponseMessage>
                  </m:ResponseMessages>
                </m:CreateItemResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var ex = Assert.Throws<InvalidOperationException>(
            () => EwsResponseParser.EnsureSuccess(XDocument.Parse(xml), "CreateItem"));
        Assert.Contains("ErrorSendAsDenied", ex.Message);
    }
}
