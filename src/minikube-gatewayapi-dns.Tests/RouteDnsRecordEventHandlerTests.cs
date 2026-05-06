using System.Net;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using k8s.GatewayApi.Model;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using minikube_gatewayapi_dns;
using minikube_gatewayapi_dns.Resolution;
using minikube_gatewayapi_dns.Watchers;
using Moq;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class RouteDnsRecordEventHandlerTests
{
    private static V1HttpRoute NewRoute(string uid, string ns, string name, string host, params ParentReference[] parents)
    {
        var r = new V1HttpRoute
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns, Uid = uid }
        };
        r.Spec.Hostnames.Add(host);
        foreach (var p in parents) r.Spec.ParentRefs.Add(p);
        return r;
    }

    private static async Task<string[]> ResolveIPs(ConcurrentMasterFile file, string host)
    {
        var req = new Request();
        req.Questions.Add(new Question(new Domain(host), RecordType.A));
        var resp = await file.Resolve(req);
        return resp.AnswerRecords.OfType<IPAddressResourceRecord>()
            .Select(r => r.IPAddress.ToString()).OrderBy(s => s).ToArray();
    }

    [Fact]
    public async Task Adds_Record_Per_Hostname_Address_Pair()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1HttpRoute>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2") });
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        handler.OnAdded(NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw" }));

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.2" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task Empty_Resolver_Result_Adds_No_Records_Fail_Closed()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1HttpRoute>())).Returns(Array.Empty<IPAddress>());
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        handler.OnAdded(NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw" }));

        Assert.Empty(await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task OnModified_Replaces_Records()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.SetupSequence(r => r.Resolve(It.IsAny<V1HttpRoute>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1") })
                .Returns(new[] { IPAddress.Parse("10.0.0.5") });
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        var route = NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw" });
        handler.OnAdded(route);
        handler.OnModified(route);

        Assert.Equal(new[] { "10.0.0.5" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task OnDeleted_Removes_Records()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1HttpRoute>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1") });
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        var route = NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw" });
        handler.OnAdded(route);
        handler.OnDeleted(route);

        Assert.Empty(await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task GatewayCache_Notification_Reresolves_Affected_Routes()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.SetupSequence(r => r.Resolve(It.IsAny<V1HttpRoute>()))
                .Returns(Array.Empty<IPAddress>())
                .Returns(new[] { IPAddress.Parse("10.0.0.7") });
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        handler.OnAdded(NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw" }));
        Assert.Empty(await ResolveIPs(file, "foo.example.com"));

        cache.AddOrUpdate(new V1Gateway
        {
            Metadata = new V1ObjectMeta { Name = "gw", NamespaceProperty = "default" }
        });

        Assert.Equal(new[] { "10.0.0.7" }, await ResolveIPs(file, "foo.example.com"));
    }

    [Fact]
    public async Task GatewayCache_Notification_For_Unrelated_Gateway_Does_Not_Reresolve()
    {
        var file = new ConcurrentMasterFile(NullLogger<ConcurrentMasterFile>.Instance);
        var resolver = new Mock<IAddressResolver<V1HttpRoute>>();
        resolver.Setup(r => r.Resolve(It.IsAny<V1HttpRoute>()))
                .Returns(new[] { IPAddress.Parse("10.0.0.1") });
        var cache = new GatewayCache();
        var handler = new RouteDnsRecordEventHandler<V1HttpRoute>(
            file, resolver.Object, new SpecHostnameSource<V1HttpRoute>(), cache, NullLogger<RouteDnsRecordEventHandler<V1HttpRoute>>.Instance);

        handler.OnAdded(NewRoute("uid-1", "default", "r", "foo.example.com",
            new ParentReference { Name = "gw-mine" }));
        resolver.Invocations.Clear();

        cache.AddOrUpdate(new V1Gateway
        {
            Metadata = new V1ObjectMeta { Name = "gw-other", NamespaceProperty = "default" }
        });

        resolver.Verify(r => r.Resolve(It.IsAny<V1HttpRoute>()), Times.Never);
    }
}
