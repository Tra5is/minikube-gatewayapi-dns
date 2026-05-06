using System.Collections.Concurrent;
using k8s.GatewayApi.Model;

namespace minikube_gatewayapi_dns;

/// <summary>
/// Thread-safe snapshot store for V1Gateway resources, keyed by (namespace, name).
/// Subscribers are expected to register once at startup and not unsubscribe concurrently
/// with notifications. Handlers must not throw — exceptions propagate and short-circuit
/// remaining subscribers.
/// </summary>
internal class GatewayCache
{
    private readonly ConcurrentDictionary<(string Namespace, string Name), V1Gateway> _gateways = new();
    private event Action<string, string>? Changed;

    public V1Gateway? TryGet(string @namespace, string name) =>
        _gateways.TryGetValue((@namespace, name), out var gw) ? gw : null;

    public void AddOrUpdate(V1Gateway gateway)
    {
        var ns = gateway.Metadata.NamespaceProperty ?? "default";
        var name = gateway.Metadata.Name;
        _gateways[(ns, name)] = gateway;
        Changed?.Invoke(ns, name);
    }

    public void Remove(string @namespace, string name)
    {
        _gateways.TryRemove((@namespace, name), out _);
        Changed?.Invoke(@namespace, name);
    }

    public void Subscribe(Action<string, string> handler) => Changed += handler;
    public void Unsubscribe(Action<string, string> handler) => Changed -= handler;
}
