using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using minikube_gatewayapi_dns;
using minikube_gatewayapi_dns.Resolution;
using minikube_gatewayapi_dns.Watchers;
using Moq;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class IngressDnsRecordEventHandlerTests
{
    private static V1Ingress NewIngress(string uid, string host, params V1IngressLoadBalancerIngress[] entries)
    {
        var ing = new V1Ingress
        {
            Metadata = new V1ObjectMeta { Name = "ing", NamespaceProperty = "default", Uid = uid },
            Spec = new V1IngressSpec
            {
                Rules = new List<V1IngressRule> { new() { Host = host } }
            }
        };
        if (entries.Length > 0)
        {
            ing.Status = new V1IngressStatus
            {
                LoadBalancer = new V1IngressLoadBalancerStatus { Ingress = entries.ToList() }
            };
        }
        return ing;
    }

    private static async Task<string[]> ResolveIPs(ConcurrentMasterFile file, string host)
    {
        var req = new Request();
        req.Questions.Add(new Question(new Domain(host), RecordType.A));
        var resp = await file.Resolve(req);
        return resp.AnswerRecords.OfType<IPAddressResourceRecord>()
            .Select(r => r.IPAddress.ToString()).OrderBy(s => s).ToArray();
    }

    private static IngressDnsRecordEventHandler NewHandler(
        ConcurrentMasterFile file, Mock<IAddressResolver<V1Ingress>> resolver, string fallbackIp = "127.0.0.1") =>
        new(file, resolver.Object, fallbackIp, NullLogger<IngressDnsRecordEventHandler>.Instance);

    [Fact]
    public async Task Resolver_With_Addresses_Adds_Records_Per_Pair()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1Ingress>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1Ingress>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2") });
        var handler = NewHandler(file, resolver);

        handler.OnAdded(NewIngress("uid-1", "foo.example.com",
            new V1IngressLoadBalancerIngress { Ip = "10.0.0.1" }));

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.2" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task Empty_Status_With_Fallback_Adds_Fallback_Record()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1Ingress>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1Ingress>())).Returns(Array.Empty<IPAddress>());
        var handler = NewHandler(file, resolver, fallbackIp: "192.168.49.2");

        handler.OnAdded(NewIngress("uid-1", "foo.example.com"));

        Assert.Equal(new[] { "192.168.49.2" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task Empty_Status_With_Empty_Fallback_Adds_No_Record()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1Ingress>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1Ingress>())).Returns(Array.Empty<IPAddress>());
        var handler = NewHandler(file, resolver, fallbackIp: "");

        handler.OnAdded(NewIngress("uid-1", "foo.example.com"));

        Assert.Empty(await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task OnModified_Replaces_Old_Records()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1Ingress>>();
        resolver.SetupSequence(r => r.Resolve(It.IsAny<V1Ingress>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1") })
                .Returns(new[] { IPAddress.Parse("10.0.0.5") });
        var handler = NewHandler(file, resolver);

        var ing = NewIngress("uid-1", "foo.example.com");
        handler.OnAdded(ing);
        handler.OnModified(ing);

        Assert.Equal(new[] { "10.0.0.5" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task OnDeleted_Clears_Records()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1Ingress>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1Ingress>())).Returns(new[] { IPAddress.Parse("10.0.0.1") });
        var handler = NewHandler(file, resolver);

        var ing = NewIngress("uid-1", "foo.example.com");
        handler.OnAdded(ing);
        handler.OnDeleted(ing);

        Assert.Empty(await ResolveIPs(file, "foo.example.com"));
    }
}
