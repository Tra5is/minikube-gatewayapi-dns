using System.Net;
using k8s.GatewayApi.Model;

namespace minikube_gatewayapi_dns.Resolution;

internal class RouteAddressResolver<TResource> : IAddressResolver<TResource>
    where TResource : IRouteResource
{
    private const string GatewayApiGroup = "gateway.networking.k8s.io";
    private const string GatewayKind = "Gateway";

    private readonly GatewayCache _gatewayCache;
    private readonly IUpstreamResolver _upstream;

    public RouteAddressResolver(GatewayCache gatewayCache, IUpstreamResolver upstream)
    {
        _gatewayCache = gatewayCache;
        _upstream = upstream;
    }

    public IEnumerable<IPAddress> Resolve(TResource resource)
    {
        var routeNamespace = resource.Metadata.NamespaceProperty ?? "default";
        foreach (var parentRef in resource.Spec.ParentRefs)
        {
            if (!IsGatewayParent(parentRef))
                continue;

            var gatewayNamespace = parentRef.Namespace ?? routeNamespace;
            var gateway = _gatewayCache.TryGet(gatewayNamespace, parentRef.Name);
            if (gateway == null)
                continue;

            foreach (var address in gateway.Status.Addresses)
            {
                if (string.Equals(address.Type, "IPAddress", StringComparison.OrdinalIgnoreCase))
                {
                    if (IPAddress.TryParse(address.Value, out var ip))
                        yield return ip;
                }
                else if (string.Equals(address.Type, "Hostname", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var resolved in _upstream.Resolve(address.Value))
                        yield return resolved;
                }
            }
        }
    }

    private static bool IsGatewayParent(ParentReference parentRef) =>
        (parentRef.Group == null || parentRef.Group == GatewayApiGroup) &&
        (parentRef.Kind == null || parentRef.Kind == GatewayKind);
}
