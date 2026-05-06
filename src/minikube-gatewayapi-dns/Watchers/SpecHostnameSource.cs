using k8s.GatewayApi.Model;

namespace minikube_gatewayapi_dns.Watchers;

internal sealed class SpecHostnameSource<TResource> : IHostnameSource<TResource>
    where TResource : IRouteResource
{
    public IEnumerable<string> GetHostnames(TResource resource) => resource.Spec.Hostnames;
}
