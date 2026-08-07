using System.Xml.Linq;
using CambioDeDomicilio.Ews;
using Xunit;

namespace CambioDeDomicilio.Tests.Ews;

public class EwsMessagesTests
{
    [Fact]
    public void BuildFindItemRequest_DistinguishedFolder_UsesDistinguishedFolderId()
    {
        var xml = EwsMessages.BuildFindItemRequest(EwsFolderRef.Distinguished("inbox"));
        var document = XDocument.Parse(xml);

        var folderRef = document.Descendants(EwsMessages.T + "DistinguishedFolderId").Single();
        Assert.Equal("inbox", folderRef.Attribute("Id")!.Value);
    }

    [Fact]
    public void BuildFindItemRequest_ResolvedFolder_UsesFolderIdAndChangeKey()
    {
        var xml = EwsMessages.BuildFindItemRequest(EwsFolderRef.ByFolderId("folder-id-1", "change-key-1"));
        var document = XDocument.Parse(xml);

        var folderRef = document.Descendants(EwsMessages.T + "FolderId").Single();
        Assert.Equal("folder-id-1", folderRef.Attribute("Id")!.Value);
        Assert.Equal("change-key-1", folderRef.Attribute("ChangeKey")!.Value);
    }

    [Fact]
    public void BuildFindItemRequest_HasNoTimeWindowRestriction()
    {
        // No Restriction element at all: trigger is "moved into the folder," not receipt time.
        var xml = EwsMessages.BuildFindItemRequest(EwsFolderRef.Distinguished("inbox"));
        var document = XDocument.Parse(xml);

        Assert.Empty(document.Descendants(EwsMessages.M + "Restriction"));
    }

    [Fact]
    public void BuildFindFolderRequest_SearchesByDisplayNameUnderMsgFolderRoot()
    {
        var xml = EwsMessages.BuildFindFolderRequest("Para pedir");
        var document = XDocument.Parse(xml);

        var constant = document.Descendants(EwsMessages.T + "Constant").Single();
        Assert.Equal("Para pedir", constant.Attribute("Value")!.Value);
        var parent = document.Descendants(EwsMessages.T + "DistinguishedFolderId").Single();
        Assert.Equal("msgfolderroot", parent.Attribute("Id")!.Value);
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

    [Fact]
    public void BuildFindItemByMessageIdRequest_FiltersByInternetMessageIdWithinFolder()
    {
        var xml = EwsMessages.BuildFindItemByMessageIdRequest(EwsFolderRef.ByFolderId("folder-1", "ck-1"), "<abc@munivalpo.cl>");
        var document = XDocument.Parse(xml);

        var constant = document.Descendants(EwsMessages.T + "Constant").Single();
        Assert.Equal("<abc@munivalpo.cl>", constant.Attribute("Value")!.Value);
        Assert.Contains(document.Descendants(EwsMessages.T + "FieldURI"),
            e => e.Attribute("FieldURI")!.Value == "message:InternetMessageId");
        var folderRef = document.Descendants(EwsMessages.T + "FolderId").Single();
        Assert.Equal("folder-1", folderRef.Attribute("Id")!.Value);
    }

    [Fact]
    public void BuildMoveItemRequest_TargetsDestinationFolderAndItem()
    {
        var xml = EwsMessages.BuildMoveItemRequest(
            new EwsItemRef("item-1", "ck-1"),
            EwsFolderRef.ByFolderId("dest-folder", "dest-ck"));
        var document = XDocument.Parse(xml);

        var moveItem = document.Descendants(EwsMessages.M + "MoveItem").Single();
        Assert.NotNull(moveItem);
        var toFolder = document.Descendants(EwsMessages.T + "FolderId").Single();
        Assert.Equal("dest-folder", toFolder.Attribute("Id")!.Value);
        var itemId = document.Descendants(EwsMessages.T + "ItemId").Single();
        Assert.Equal("item-1", itemId.Attribute("Id")!.Value);
        Assert.Equal("ck-1", itemId.Attribute("ChangeKey")!.Value);
    }

    [Fact]
    public void BuildUpdateItemSetReadFlagRequest_SetsIsReadField()
    {
        var xml = EwsMessages.BuildUpdateItemSetReadFlagRequest(new EwsItemRef("item-1", "ck-1"), isRead: false);
        var document = XDocument.Parse(xml);

        var updateItem = document.Descendants(EwsMessages.M + "UpdateItem").Single();
        Assert.Equal("AlwaysOverwrite", updateItem.Attribute("ConflictResolution")!.Value);
        Assert.Equal("false", document.Descendants(EwsMessages.T + "IsRead").Single().Value);
    }
}
