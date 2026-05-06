using System.Net;
using Microsoft.Extensions.Logging;

namespace minikube_gatewayapi_dns.Resolution;

internal class SystemUpstreamResolver : IUpstreamResolver
{
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<SystemUpstreamResolver> _logger;

    public SystemUpstreamResolver(ILogger<SystemUpstreamResolver> logger)
    {
        _logger = logger;
    }

    public IPAddress[] Resolve(string hostname)
    {
        try
        {
            using var cts = new CancellationTokenSource(ResolveTimeout);
            var addresses = Dns.GetHostAddressesAsync(hostname, cts.Token).GetAwaiter().GetResult();
            return addresses ?? Array.Empty<IPAddress>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Upstream DNS resolution failed for {Hostname}: {Message}", hostname, ex.Message);
            return Array.Empty<IPAddress>();
        }
    }
}
