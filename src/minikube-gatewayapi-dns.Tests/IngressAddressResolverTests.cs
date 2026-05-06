using System.Net;
using k8s.Models;
using minikube_gatewayapi_dns.Resolution;
using Moq;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class IngressAddressResolverTests
{
    private static V1Ingress NewIngress(params V1IngressLoadBalancerIngress[] entries)
    {
        var ing = new V1Ingress { Metadata = new V1ObjectMeta { Name = "ing", NamespaceProperty = "default" } };
        if (entries.Length > 0)
        {
            ing.Status = new V1IngressStatus
            {
                LoadBalancer = new V1IngressLoadBalancerStatus
                {
                    Ingress = entries.ToList()
                }
            };
        }
        return ing;
    }

    [Fact]
    public void Returns_IP_For_Entries_With_Ip_Set()
    {
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new IngressAddressResolver(upstream.Object);
        var ing = NewIngress(new V1IngressLoadBalancerIngress { Ip = "10.0.0.1" });

        var result = resolver.Resolve(ing).ToArray();

        Assert.Single(result);
        Assert.Equal("10.0.0.1", result[0].ToString());
    }

    [Fact]
    public void Translates_Hostname_Via_Upstream_Resolver()
    {
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        upstream.Setup(u => u.Resolve("lb.example.com")).Returns(new[] { IPAddress.Parse("10.0.0.7") });
        var resolver = new IngressAddressResolver(upstream.Object);
        var ing = NewIngress(new V1IngressLoadBalancerIngress { Hostname = "lb.example.com" });

        var result = resolver.Resolve(ing).ToArray();

        Assert.Single(result);
        Assert.Equal("10.0.0.7", result[0].ToString());
        upstream.VerifyAll();
    }

    [Fact]
    public void Returns_Empty_When_Status_Is_Empty()
    {
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        var resolver = new IngressAddressResolver(upstream.Object);
        var ing = NewIngress();

        Assert.Empty(resolver.Resolve(ing));
    }

    [Fact]
    public void Returns_Multiple_Addresses_When_Status_Has_Multiple()
    {
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        upstream.Setup(u => u.Resolve("lb.example.com")).Returns(new[] { IPAddress.Parse("10.0.0.7") });
        var resolver = new IngressAddressResolver(upstream.Object);
        var ing = NewIngress(
            new V1IngressLoadBalancerIngress { Ip = "10.0.0.1" },
            new V1IngressLoadBalancerIngress { Hostname = "lb.example.com" });

        var result = resolver.Resolve(ing).Select(ip => ip.ToString()).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.7" }, result);
    }

    [Fact]
    public void Skips_Entries_Where_Upstream_Returns_Empty()
    {
        var upstream = new Mock<IUpstreamResolver>(MockBehavior.Strict);
        upstream.Setup(u => u.Resolve("lb.example.com")).Returns(Array.Empty<IPAddress>());
        var resolver = new IngressAddressResolver(upstream.Object);
        var ing = NewIngress(
            new V1IngressLoadBalancerIngress { Ip = "10.0.0.1" },
            new V1IngressLoadBalancerIngress { Hostname = "lb.example.com" });

        var result = resolver.Resolve(ing).Select(ip => ip.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.1" }, result);
    }
}
