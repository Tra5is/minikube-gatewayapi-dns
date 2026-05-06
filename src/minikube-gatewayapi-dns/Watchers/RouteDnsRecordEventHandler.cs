using System.Collections.Concurrent;
using DNS.Protocol;
using k8s.GatewayApi.Model;
using k8s.Models;
using Microsoft.Extensions.Logging;
using minikube_gatewayapi_dns.Resolution;

namespace minikube_gatewayapi_dns.Watchers;

internal class RouteDnsRecordEventHandler<TResource> : IWatchEventHandler<TResource>, IDisposable
    where TResource : IRouteResource
{
    private const string GatewayApiGroup = "gateway.networking.k8s.io";
    private const string GatewayKind = "Gateway";

    private readonly ConcurrentMasterFile _masterFile;
    private readonly IAddressResolver<TResource> _resolver;
    private readonly GatewayCache _gatewayCache;
    private readonly ILogger<RouteDnsRecordEventHandler<TResource>> _logger;
    private readonly ConcurrentDictionary<string, TResource> _knownRoutes = new();

    public RouteDnsRecordEventHandler(
        ConcurrentMasterFile masterFile,
        IAddressResolver<TResource> resolver,
        GatewayCache gatewayCache,
        ILogger<RouteDnsRecordEventHandler<TResource>> logger)
    {
        _masterFile = masterFile;
        _resolver = resolver;
        _gatewayCache = gatewayCache;
        _logger = logger;
        _gatewayCache.Subscribe(OnGatewayChanged);
    }

    public void OnAdded(TResource resource)
    {
        _knownRoutes[resource.Uid()] = resource;
        ResolveAndPublish(resource);
    }

    public void OnModified(TResource resource)
    {
        _knownRoutes[resource.Uid()] = resource;
        _masterFile.RemoveIPAddressResourceRecord(resource.Uid());
        ResolveAndPublish(resource);
    }

    public void OnDeleted(TResource resource)
    {
        _knownRoutes.TryRemove(resource.Uid(), out _);
        _masterFile.RemoveIPAddressResourceRecord(resource.Uid());
    }

    private void ResolveAndPublish(TResource resource)
    {
        var addresses = _resolver.Resolve(resource).ToList();
        if (addresses.Count == 0)
        {
            _logger.LogTrace(
                "Route {Namespace}/{Name} has no resolvable addresses; deferring until parent Gateway publishes status",
                resource.Metadata.NamespaceProperty, resource.Metadata.Name);
            return;
        }

        var resourceId = resource.Uid();
        foreach (var host in resource.Spec.Hostnames)
        foreach (var addr in addresses)
        {
            _masterFile.AddIPAddressResourceRecord(resourceId, new Domain(host), addr);
            _logger.LogInformation(
                "Adding DNS record {Host} -> {Address} for {Kind} {Namespace}/{Name}",
                host, addr, typeof(TResource).Name, resource.Metadata.NamespaceProperty, resource.Metadata.Name);
        }
    }

    private void OnGatewayChanged(string gatewayNamespace, string gatewayName)
    {
        foreach (var route in _knownRoutes.Values.ToList())
        {
            if (!RouteReferences(route, gatewayNamespace, gatewayName))
                continue;

            _masterFile.RemoveIPAddressResourceRecord(route.Uid());
            ResolveAndPublish(route);
        }
    }

    private static bool RouteReferences(TResource route, string gatewayNamespace, string gatewayName)
    {
        var routeNamespace = route.Metadata.NamespaceProperty ?? "default";
        return route.Spec.ParentRefs.Any(pr =>
            (pr.Group == null || pr.Group == GatewayApiGroup) &&
            (pr.Kind == null || pr.Kind == GatewayKind) &&
            pr.Name == gatewayName &&
            (pr.Namespace ?? routeNamespace) == gatewayNamespace);
    }

    public void Dispose()
    {
        _gatewayCache.Unsubscribe(OnGatewayChanged);
    }
}
