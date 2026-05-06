using k8s;
using k8s.Models;

namespace minikube_gatewayapi_dns.Watchers;

// TCPRoute / UDPRoute have no native hostnames field. Mirror ExternalDNS's convention:
// metadata.annotations["external-dns.alpha.kubernetes.io/hostname"] holds one or more
// comma-separated hostnames.
internal sealed class AnnotationHostnameSource<TResource> : IHostnameSource<TResource>
    where TResource : IKubernetesObject<V1ObjectMeta>
{
    public const string HostnameAnnotation = "external-dns.alpha.kubernetes.io/hostname";

    public IEnumerable<string> GetHostnames(TResource resource)
    {
        var annotations = resource.Metadata?.Annotations;
        if (annotations is null || !annotations.TryGetValue(HostnameAnnotation, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(h => h.Length > 0);
    }
}
