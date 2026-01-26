using k8s.Models;

namespace k8s.GatewayApi.Model
{
    [KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "HTTPRoute", ApiVersion = "v1", PluralName = "httproutes")]
    public class HttpRoute : IKubernetesObject<V1ObjectMeta>
    {
        public V1ObjectMeta Metadata { get; set; }
        public string ApiVersion { get; set; }
        public string Kind { get; set; }
        public HttpRouteSpec Spec { get; set; }
    }

    public class HttpRouteSpec
    {
        public List<string> Hostnames { get; set; }
    }
}
