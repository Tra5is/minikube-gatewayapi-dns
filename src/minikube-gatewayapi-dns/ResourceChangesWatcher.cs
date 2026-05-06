using k8s;
using k8s.GatewayApi.Model.Extensions;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using minikube_gatewayapi_dns.Watchers;

namespace minikube_gatewayapi_dns
{
    internal class ResourceChangesWatcher<TResource> : BackgroundService
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly IWatchEventHandler<TResource> _handler;
        private readonly ILogger<ResourceChangesWatcher<TResource>> _logger;
        private readonly GenericClient _typedClient;

        public ResourceChangesWatcher(
            IWatchEventHandler<TResource> handler,
            ILogger<ResourceChangesWatcher<TResource>> logger)
        {
            _handler = handler;
            _logger = logger;

            var config = IsRunningInKubePod()
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile();
            var client = new Kubernetes(config);
            _typedClient = new GenericClient(client,
                KubernetesObjectExtensions.GetKubernetesEntityGroup<TResource>(),
                KubernetesObjectExtensions.GetKubernetesEntityVersion<TResource>(),
                KubernetesObjectExtensions.GetKubernetesEntityPluralName<TResource>());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested)
                return;

            while (await IsResourceFoundAsync(stoppingToken) == false)
            {
                _logger.LogWarning(
                    "The resource type {ResourceType} cannot be found. Waiting 10 seconds before trying again...",
                    typeof(TResource).Name);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            await RetryOnException(stoppingToken, WatchResourceChanges);
        }

        private async Task RetryOnException(CancellationToken cancellationToken, Func<CancellationToken, Task> doThis)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await doThis(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception watching resource {ResourceType}: {Message} ({Type})",
                        typeof(TResource).Name, ex.Message, ex.GetType().Name);
                    _logger.LogTrace(ex, "{Message}", ex.Message);
                }
            }
        }

        private async Task WatchResourceChanges(CancellationToken stoppingToken)
        {
            var resources = _typedClient.WatchAsync<TResource>(cancel: stoppingToken);

            _logger.LogInformation("Watching for changes to {ResourceType}...", typeof(TResource).Name);

            await foreach (var (watchEventType, resource) in resources)
            {
                _logger.LogTrace("watchedEvent {EventType} : {Resource}",
                    watchEventType, System.Text.Json.JsonSerializer.Serialize(resource));

                switch (watchEventType)
                {
                    case WatchEventType.Added:
                        _handler.OnAdded(resource);
                        break;
                    case WatchEventType.Modified:
                        _handler.OnModified(resource);
                        break;
                    case WatchEventType.Deleted:
                        _handler.OnDeleted(resource);
                        break;
                    default:
                        _logger.LogTrace("Unhandled watch event type: {EventType} for {Kind}",
                            watchEventType, resource.Kind);
                        break;
                }
            }
        }

        private async Task<bool> IsResourceFoundAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _typedClient.ListAsync<TResource>(cancellationToken);
                return true;
            }
            catch (k8s.Autorest.HttpOperationException ex)
            {
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;
                throw;
            }
        }

        private static bool IsRunningInKubePod() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_PORT"));
    }
}
