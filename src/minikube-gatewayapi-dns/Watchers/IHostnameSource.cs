namespace minikube_gatewayapi_dns.Watchers;

internal interface IHostnameSource<in TResource>
{
    IEnumerable<string> GetHostnames(TResource resource);
}
