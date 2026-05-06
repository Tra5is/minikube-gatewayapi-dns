namespace k8s.GatewayApi.Model;

// Shared spec shape for HTTPRoute and GRPCRoute. Both gateway.networking.k8s.io/v1
// route kinds carry the same hostnames + parentRefs structure; we only need those fields.
public class RouteSpec
{
    public List<string> Hostnames { get; set; } = [];
    public List<ParentReference> ParentRefs { get; set; } = [];
}
