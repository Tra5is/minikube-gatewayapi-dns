namespace minikube_gatewayapi_dns.Watchers;

// Strategy invoked by ResourceChangesWatcher<TResource> on each watch event.
// Implementations decide what to do with the event: produce DNS records,
// update an in-memory cache, etc.
public interface IWatchEventHandler<in TResource>
{
    void OnAdded(TResource resource);
    void OnModified(TResource resource);
    void OnDeleted(TResource resource);
}
