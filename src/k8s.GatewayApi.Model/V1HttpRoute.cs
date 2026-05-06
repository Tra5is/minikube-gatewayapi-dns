using k8s.Models;

namespace k8s.GatewayApi.Model
{
    [KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "HTTPRoute", ApiVersion = "v1", PluralName = "httproutes")]
    public class V1HttpRoute : IRouteResource
    {
        public V1ObjectMeta Metadata { get; set; } = new();
        public string ApiVersion { get; set; } = "";
        public string Kind { get; set; } = "";
        public RouteSpec Spec { get; set; } = new();
    }
}
