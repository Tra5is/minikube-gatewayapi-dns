using System.Net;

namespace minikube_gatewayapi_dns.Resolution;

// Wraps system DNS for testability. Stateless. Returns empty array on timeout/error rather than throwing.
public interface IUpstreamResolver
{
    IPAddress[] Resolve(string hostname);
}
