namespace k8s.GatewayApi.Model;

// Mirrors gateway.networking.k8s.io/v1 ParentReference.
// Used by HTTPRoute and GRPCRoute spec.parentRefs.
public class ParentReference
{
    public string? Group { get; set; }
    public string? Kind { get; set; }
    public string? Namespace { get; set; }
    public string Name { get; set; } = "";
    public string? SectionName { get; set; }
}
