using k8s.Models;

namespace k8s.GatewayApi.Model;

[KubernetesEntity(Group = "gateway.networking.k8s.io", Kind = "Gateway", ApiVersion = "v1", PluralName = "gateways")]
public class V1Gateway : IKubernetesObject<V1ObjectMeta>
{
    public V1ObjectMeta Metadata { get; set; } = new();
    public string ApiVersion { get; set; } = "";
    public string Kind { get; set; } = "";
    public GatewaySpec Spec { get; set; } = new();
    public GatewayStatus Status { get; set; } = new();
}

public class GatewaySpec
{
    public string GatewayClassName { get; set; } = "";
}

public class GatewayStatus
{
    public List<GatewayStatusAddress> Addresses { get; set; } = [];
}

public class GatewayStatusAddress
{
    public string Type { get; set; } = "IPAddress";
    public string Value { get; set; } = "";
}
