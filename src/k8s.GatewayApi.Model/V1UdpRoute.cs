using k8s.Models;

namespace k8s.GatewayApi.Model
{
    [KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "UDPRoute", ApiVersion = "v1alpha2", PluralName = "udproutes")]
    public class V1UdpRoute : IRouteResource
    {
        public V1ObjectMeta Metadata { get; set; } = new();
        public string ApiVersion { get; set; } = "";
        public string Kind { get; set; } = "";
        public RouteSpec Spec { get; set; } = new();
    }
}
