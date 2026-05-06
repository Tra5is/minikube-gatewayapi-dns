using k8s.GatewayApi.Model;
using k8s.Models;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class GatewayCacheTests
{
    private static V1Gateway NewGateway(string ns, string name, params (string Type, string Value)[] addresses)
    {
        var gw = new V1Gateway
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns, Uid = $"{ns}/{name}" },
        };
        foreach (var (type, value) in addresses)
            gw.Status.Addresses.Add(new GatewayStatusAddress { Type = type, Value = value });
        return gw;
    }

    [Fact]
    public void TryGet_Returns_Stored_Snapshot()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw-a", ("IPAddress", "10.0.0.1")));

        var got = cache.TryGet("default", "gw-a");

        Assert.NotNull(got);
        Assert.Single(got!.Status.Addresses);
        Assert.Equal("10.0.0.1", got.Status.Addresses[0].Value);
    }

    [Fact]
    public void TryGet_Returns_Null_For_Unknown_Gateway()
    {
        var cache = new GatewayCache();
        Assert.Null(cache.TryGet("default", "missing"));
    }

    [Fact]
    public void Update_Replaces_Snapshot_And_Notifies()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw-a"));

        var notifications = new List<(string, string)>();
        cache.Subscribe((ns, name) => notifications.Add((ns, name)));

        cache.AddOrUpdate(NewGateway("default", "gw-a", ("IPAddress", "10.0.0.5")));

        Assert.Equal("10.0.0.5", cache.TryGet("default", "gw-a")!.Status.Addresses[0].Value);
        Assert.Single(notifications);
        Assert.Equal(("default", "gw-a"), notifications[0]);
    }

    [Fact]
    public void Remove_Drops_Snapshot_And_Notifies()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw-a"));

        var notifications = new List<(string, string)>();
        cache.Subscribe((ns, name) => notifications.Add((ns, name)));

        cache.Remove("default", "gw-a");

        Assert.Null(cache.TryGet("default", "gw-a"));
        Assert.Single(notifications);
        Assert.Equal(("default", "gw-a"), notifications[0]);
    }

    [Fact]
    public void Multiple_Subscribers_All_Notified()
    {
        var cache = new GatewayCache();
        var a = 0;
        var b = 0;
        cache.Subscribe((_, _) => a++);
        cache.Subscribe((_, _) => b++);

        cache.AddOrUpdate(NewGateway("default", "gw-a"));

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void Unsubscribe_Stops_Notifications()
    {
        var cache = new GatewayCache();
        var count = 0;
        Action<string, string> handler = (_, _) => count++;
        cache.Subscribe(handler);
        cache.AddOrUpdate(NewGateway("default", "gw-a"));
        cache.Unsubscribe(handler);
        cache.AddOrUpdate(NewGateway("default", "gw-a"));

        Assert.Equal(1, count);
    }
}
