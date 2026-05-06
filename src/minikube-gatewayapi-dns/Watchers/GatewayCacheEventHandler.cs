using k8s.GatewayApi.Model;

namespace minikube_gatewayapi_dns.Watchers;

internal class GatewayCacheEventHandler : IWatchEventHandler<V1Gateway>
{
    private readonly GatewayCache _cache;

    public GatewayCacheEventHandler(GatewayCache cache)
    {
        _cache = cache;
    }

    public void OnAdded(V1Gateway resource) => _cache.AddOrUpdate(resource);
    public void OnModified(V1Gateway resource) => _cache.AddOrUpdate(resource);

    public void OnDeleted(V1Gateway resource)
    {
        var ns = resource.Metadata.NamespaceProperty ?? "default";
        _cache.Remove(ns, resource.Metadata.Name);
    }
}
