using k8s.GatewayApi.Model;
using k8s.Models;
using minikube_gatewayapi_dns.Watchers;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class GatewayCacheEventHandlerTests
{
    private static V1Gateway NewGateway(string ns, string name, string addr = "10.0.0.1") =>
        new V1Gateway
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns, Uid = $"{ns}/{name}" },
            Status = new GatewayStatus
            {
                Addresses = { new GatewayStatusAddress { Type = "IPAddress", Value = addr } }
            }
        };

    [Fact]
    public void OnAdded_Stores_In_Cache()
    {
        var cache = new GatewayCache();
        var handler = new GatewayCacheEventHandler(cache);

        handler.OnAdded(NewGateway("default", "gw"));

        Assert.NotNull(cache.TryGet("default", "gw"));
    }

    [Fact]
    public void OnModified_Updates_Cache()
    {
        var cache = new GatewayCache();
        var handler = new GatewayCacheEventHandler(cache);
        handler.OnAdded(NewGateway("default", "gw", "10.0.0.1"));

        handler.OnModified(NewGateway("default", "gw", "10.0.0.5"));

        Assert.Equal("10.0.0.5", cache.TryGet("default", "gw")!.Status.Addresses[0].Value);
    }

    [Fact]
    public void OnDeleted_Removes_From_Cache()
    {
        var cache = new GatewayCache();
        var handler = new GatewayCacheEventHandler(cache);
        handler.OnAdded(NewGateway("default", "gw"));

        handler.OnDeleted(NewGateway("default", "gw"));

        Assert.Null(cache.TryGet("default", "gw"));
    }
}
