using k8s.Models;

namespace k8s.GatewayApi.Model;

// Lets a single resolver class handle both V1HttpRoute and V1GrpcRoute generically.
public interface IRouteResource : IKubernetesObject<V1ObjectMeta>
{
    RouteSpec Spec { get; }
}
