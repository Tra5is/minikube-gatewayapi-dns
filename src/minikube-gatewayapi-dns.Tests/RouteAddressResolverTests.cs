using System.Net;
using k8s.GatewayApi.Model;
using k8s.Models;
using minikube_gatewayapi_dns.Resolution;
using Moq;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class RouteAddressResolverTests
{
    private static V1HttpRoute NewRoute(string ns, string name, params ParentReference[] parents)
    {
        var route = new V1HttpRoute
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns, Uid = $"{ns}/{name}" }
        };
        foreach (var p in parents) route.Spec.ParentRefs.Add(p);
        return route;
    }

    private static V1Gateway NewGateway(string ns, string name, params (string Type, string Value)[] addrs)
    {
        var gw = new V1Gateway { Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns } };
        foreach (var (t, v) in addrs)
            gw.Status.Addresses.Add(new GatewayStatusAddress { Type = t, Value = v });
        return gw;
    }

    [Fact]
    public void Single_Parent_Single_Address_Returns_One_IP()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r", new ParentReference { Name = "gw" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.1" }, result);
    }

    [Fact]
    public void Multiple_Addresses_From_Single_Parent_All_Returned()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw",
            ("IPAddress", "10.0.0.1"), ("IPAddress", "10.0.0.2")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r", new ParentReference { Name = "gw" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.2" }, result);
    }

    [Fact]
    public void Multiple_Parents_Addresses_From_Each_Returned()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw1", ("IPAddress", "10.0.0.1")));
        cache.AddOrUpdate(NewGateway("default", "gw2", ("IPAddress", "10.0.0.2")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r",
            new ParentReference { Name = "gw1" },
            new ParentReference { Name = "gw2" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.2" }, result);
    }

    [Fact]
    public void Parent_Not_In_Cache_Is_Skipped_Other_Parents_Resolve()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw1", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r",
            new ParentReference { Name = "gw1" },
            new ParentReference { Name = "missing" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.1" }, result);
    }

    [Fact]
    public void Hostname_Address_Translated_Via_Upstream()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw", ("Hostname", "lb.example.com")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        upstream.Setup(u => u.Resolve("lb.example.com")).Returns(new[] { IPAddress.Parse("10.0.0.9") });
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r", new ParentReference { Name = "gw" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.9" }, result);
        upstream.VerifyAll();
    }

    [Fact]
    public void Non_Gateway_ParentRef_Kind_Is_Ignored()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r",
            new ParentReference { Kind = "Service", Name = "gw" });
        var result = resolver.Resolve(route).ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void ParentRef_Group_Mismatch_Is_Ignored()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("default", "gw", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r",
            new ParentReference { Group = "other.example.com", Name = "gw" });
        var result = resolver.Resolve(route).ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void Cross_Namespace_ParentRef_Resolves_From_Specified_Namespace()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("ingress-system", "gw", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("default", "r",
            new ParentReference { Namespace = "ingress-system", Name = "gw" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.1" }, result);
    }

    [Fact]
    public void Same_Namespace_Default_When_ParentRef_Namespace_Omitted()
    {
        var cache = new GatewayCache();
        cache.AddOrUpdate(NewGateway("my-ns", "gw", ("IPAddress", "10.0.0.1")));
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new RouteAddressResolver<V1HttpRoute>(cache, upstream.Object);

        var route = NewRoute("my-ns", "r", new ParentReference { Name = "gw" });
        var result = resolver.Resolve(route).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.1" }, result);
    }
}
