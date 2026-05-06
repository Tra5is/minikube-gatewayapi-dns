using System.Net;
using k8s.Models;

namespace minikube_gatewayapi_dns.Resolution;

internal class IngressAddressResolver : IAddressResolver<V1Ingress>
{
    private readonly IUpstreamResolver _upstream;

    public IngressAddressResolver(IUpstreamResolver upstream)
    {
        _upstream = upstream;
    }

    public IEnumerable<IPAddress> Resolve(V1Ingress resource)
    {
        var entries = resource.Status?.LoadBalancer?.Ingress;
        if (entries == null)
            yield break;

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Ip) && IPAddress.TryParse(entry.Ip, out var ip))
            {
                yield return ip;
            }
            else if (!string.IsNullOrEmpty(entry.Hostname))
            {
                foreach (var resolved in _upstream.Resolve(entry.Hostname))
                    yield return resolved;
            }
        }
    }
}
