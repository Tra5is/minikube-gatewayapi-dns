using Microsoft.Extensions.Logging.Abstractions;
using minikube_gatewayapi_dns.Resolution;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class SystemUpstreamResolverTests
{
    [Fact]
    public void Returns_Empty_For_Unresolvable_Name()
    {
        var resolver = new SystemUpstreamResolver(NullLogger<SystemUpstreamResolver>.Instance);
        var result = resolver.Resolve("does-not-exist.invalid");
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_Loopback_For_Localhost()
    {
        var resolver = new SystemUpstreamResolver(NullLogger<SystemUpstreamResolver>.Instance);
        var result = resolver.Resolve("localhost");
        Assert.NotEmpty(result);
    }
}
