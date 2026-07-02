using System.Xml.Linq;
using OutlookComunaRouter.Ews;
using Xunit;

namespace OutlookComunaRouter.Tests.Ews;

public class EwsMessagesTests
{
    [Fact]
    public void BuildFindItemRequest_IncludesSinceDateInUtc()
    {
        var since = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-4));

        var xml = EwsMessages.BuildFindItemRequest(since);
        var document = XDocument.Parse(xml);

        var constant = document.Descendants(EwsMessages.T + "Constant").Single();
        Assert.Equal("2026-07-01T16:00:00Z", constant.Attribute("Value")!.Value);
    }

    [Fact]
    public void BuildSendMailRequest_EscapesXmlSpecialCharacters()
    {
        var xml = EwsMessages.BuildSendMailRequest(
            "destino@municatemu.cl",
            "Solicitud <urgente> & formal",
            "Cuerpo con <tags> & ampersands");

        var document = XDocument.Parse(xml); // parses without error = properly escaped
        var subject = document.Descendants(EwsMessages.T + "Subject").Single();
        Assert.Equal("Solicitud <urgente> & formal", subject.Value);
    }

    [Fact]
    public void BuildSendMailRequest_SetsSendAndSaveCopyDisposition()
    {
        var xml = EwsMessages.BuildSendMailRequest("a@b.cl", "s", "b");
        var document = XDocument.Parse(xml);

        var createItem = document.Descendants(EwsMessages.M + "CreateItem").Single();
        Assert.Equal("SendAndSaveCopy", createItem.Attribute("MessageDisposition")!.Value);
    }

    [Fact]
    public void BuildGetItemRequest_RequestsTextBodyAndInternetMessageId()
    {
        var xml = EwsMessages.BuildGetItemRequest([("id-1", "ck-1")]);
        var document = XDocument.Parse(xml);

        Assert.Equal("Text", document.Descendants(EwsMessages.T + "BodyType").Single().Value);
        Assert.Contains(document.Descendants(EwsMessages.T + "FieldURI"),
            e => e.Attribute("FieldURI")!.Value == "message:InternetMessageId");
    }
}
