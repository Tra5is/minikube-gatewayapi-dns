using k8s.Models;

namespace k8s.GatewayApi.Model
{
    [KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "GRPCRoute", ApiVersion = "v1", PluralName = "grpcroutes")]
    public class V1GrpcRoute : IKubernetesObject<V1ObjectMeta>
    {
        public V1ObjectMeta Metadata { get; set; } = new();
        public string ApiVersion { get; set; } = "";
        public string Kind { get; set; } = "";
        public GrpcRouteSpec Spec { get; set; } = new();
    }

    public class GrpcRouteSpec
    {
        public List<string> Hostnames { get; set; } = [];
    }
}
