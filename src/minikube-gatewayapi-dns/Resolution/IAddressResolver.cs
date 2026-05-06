using System.Net;

namespace minikube_gatewayapi_dns.Resolution;

// Stateless. Reads a resource's status (and any cached parent state) and returns the
// addresses where its hostnames should be served. No internal cache; the master file is
// the only persistence layer.
public interface IAddressResolver<in TResource>
{
    IEnumerable<IPAddress> Resolve(TResource resource);
}
