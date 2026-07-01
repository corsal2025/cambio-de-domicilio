using Azure.Identity;
using Microsoft.Graph;
using OutlookComunaRouter.Configuration;

namespace OutlookComunaRouter.Graph;

public interface IGraphClientFactory
{
    GraphServiceClient Create();
}

public sealed class GraphClientFactory(RouterOptions options) : IGraphClientFactory
{
    public GraphServiceClient Create()
    {
        var credential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }
}
