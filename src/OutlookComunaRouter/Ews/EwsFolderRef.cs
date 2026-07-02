namespace OutlookComunaRouter.Ews;

/// <summary>A folder identified either by a well-known distinguished ID (e.g. "inbox") or a resolved FolderId+ChangeKey.</summary>
public sealed record EwsFolderRef(string? DistinguishedId, string? Id, string? ChangeKey)
{
    public static EwsFolderRef Distinguished(string id) => new(id, null, null);

    public static EwsFolderRef ByFolderId(string id, string changeKey) => new(null, id, changeKey);
}
