using k8s.Models;

namespace k8s.GatewayApi.Model
{
    [KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "TCPRoute", ApiVersion = "v1alpha2", PluralName = "tcproutes")]
    public class V1TcpRoute : IRouteResource
    {
        public V1ObjectMeta Metadata { get; set; } = new();
        public string ApiVersion { get; set; } = "";
        public string Kind { get; set; } = "";
        public RouteSpec Spec { get; set; } = new();
    }
}
