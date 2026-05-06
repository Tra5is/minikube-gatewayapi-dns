using k8s.Models;
using Microsoft.Extensions.Logging;
using minikube_gatewayapi_dns.Resolution;

namespace minikube_gatewayapi_dns.Watchers;

internal class IngressDnsRecordEventHandler : IWatchEventHandler<V1Ingress>
{
    private readonly ConcurrentMasterFile _masterFile;
    private readonly IAddressResolver<V1Ingress> _resolver;
    private readonly string _fallbackIp;
    private readonly ILogger<IngressDnsRecordEventHandler> _logger;

    public IngressDnsRecordEventHandler(
        ConcurrentMasterFile masterFile,
        IAddressResolver<V1Ingress> resolver,
        string fallbackIp,
        ILogger<IngressDnsRecordEventHandler> logger)
    {
        _masterFile = masterFile;
        _resolver = resolver;
        _fallbackIp = fallbackIp;
        _logger = logger;
    }

    public void OnAdded(V1Ingress resource)
    {
        var resourceId = resource.Uid();
        var hostnames = (resource.Spec?.Rules ?? new List<V1IngressRule>())
            .Select(r => r.Host)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToArray();
        if (hostnames.Length == 0)
            return;

        var addresses = _resolver.Resolve(resource).ToList();

        if (addresses.Count == 0)
        {
            if (string.IsNullOrEmpty(_fallbackIp))
            {
                _logger.LogWarning(
                    "Ingress {Namespace}/{Name} has empty status and no POD_IP fallback; no DNS record added",
                    resource.Metadata.NamespaceProperty, resource.Metadata.Name);
                return;
            }
            _logger.LogInformation(
                "Ingress {Namespace}/{Name} has empty status; falling back to POD_IP {PodIp}",
                resource.Metadata.NamespaceProperty, resource.Metadata.Name, _fallbackIp);
            foreach (var host in hostnames)
                _masterFile.AddIPAddressResourceRecord(resourceId, host, _fallbackIp);
            return;
        }

        foreach (var host in hostnames)
        foreach (var addr in addresses)
        {
            _masterFile.AddIPAddressResourceRecord(resourceId, new DNS.Protocol.Domain(host), addr);
            _logger.LogInformation(
                "Adding DNS record {Host} -> {Address} for Ingress {Namespace}/{Name}",
                host, addr, resource.Metadata.NamespaceProperty, resource.Metadata.Name);
        }
    }

    public void OnModified(V1Ingress resource)
    {
        OnDeleted(resource);
        OnAdded(resource);
    }

    public void OnDeleted(V1Ingress resource)
    {
        _masterFile.RemoveIPAddressResourceRecord(resource.Uid());
    }
}
