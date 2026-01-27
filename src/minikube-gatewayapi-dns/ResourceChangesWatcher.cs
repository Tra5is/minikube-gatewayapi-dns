using k8s;
using k8s.GatewayApi.Model;
using k8s.GatewayApi.Model.Extensions;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace minikube_gatewayapi_dns
{
    internal class ResourceChangesWatcher<TResource> : BackgroundService
        where TResource : IKubernetesObject
    {
        private readonly ConcurrentMasterFile _masterFile;
        private readonly ILogger<ResourceChangesWatcher<TResource>> _logger;
        private readonly GenericClient _typedClient;
        private readonly string _dnsServerIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";
        
        public ResourceChangesWatcher(ConcurrentMasterFile masterFile, ILogger<ResourceChangesWatcher<TResource>> logger)
        {
            _masterFile = masterFile;
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
                _logger.LogWarning($"The resource type {typeof(TResource).Name} cannot be found. Waiting 10 seconds before trying again...");
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
                    _logger.LogError($"Exception watching resource {typeof(TResource).Name}: {ex.Message} ({ex.GetType().Name})");
                    _logger.LogTrace(ex, ex.Message);
                }
            }
        }

        private async Task WatchResourceChanges(CancellationToken stoppingToken)
        {
            var resources =
                _typedClient.WatchAsync<TResource>(cancel: stoppingToken);

            _logger.LogInformation($"Watching for changes to {typeof(TResource).Name}...");

            await foreach (var (watchEventType, resource) in resources)
            {
                _logger.LogTrace(
                    $"watchedEvent {watchEventType} : {System.Text.Json.JsonSerializer.Serialize(resource)}");

                if (watchEventType == WatchEventType.Added)
                    HandleAdded(resource);
                else if (watchEventType == WatchEventType.Modified)
                    HandleModified(resource);
                else if (watchEventType == WatchEventType.Deleted)
                    HandleDeleted(resource);
                else
                    _logger.LogTrace($"Unhandled watch event type: {watchEventType} for {resource.Kind}");
            }
        }

        private void HandleAdded(TResource resource)
        {
            var hostnames = GetHostnames(resource);

            foreach (var host in hostnames)
            {
                _logger.LogInformation($"Creating DNS entry for {host} to point to {_dnsServerIp}");
                _masterFile.AddIPAddressResourceRecord(GetResourceId(resource), host, _dnsServerIp);
            }
        }

        private void HandleDeleted(TResource resource)
        {
            _logger.LogInformation($"Removing DNS entries for {typeof(TResource).Name}: {GetResourceName(resource)}");
            _masterFile.RemoveIPAddressResourceRecord(GetResourceId(resource));
        }

        private void HandleModified(TResource resource)
        {
            _logger.LogInformation($"{typeof(TResource).Name}: {GetResourceName(resource)} is modified");
            HandleDeleted(resource);
            HandleAdded(resource);
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

        private bool IsRunningInKubePod() => 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_PORT"));

        private string[] GetHostnames(object resource) =>
            resource switch
            {
                V1HttpRoute httpRoute => httpRoute.Spec.Hostnames.ToArray(),
                V1GrpcRoute grpcRoute => grpcRoute.Spec.Hostnames.ToArray(),
                V1Ingress v1Ingress => v1Ingress.Spec.Rules.Select(rule => rule.Host).ToArray(),
                _ => throw new InvalidOperationException($"GetHostnames: Unexpected type of resource {resource.GetType().Name}")
            };

        private string GetResourceId(object resource) =>
            resource switch
            {
                V1HttpRoute httpRoute => httpRoute.Uid(),
                V1GrpcRoute grpcRoute => grpcRoute.Uid(),
                V1Ingress v1Ingress => v1Ingress.Uid(),
                _ => throw new InvalidOperationException($"GetResourceId: Unexpected type of resource {resource.GetType().Name}")
            };

        private string GetResourceName(object resource) =>
            resource switch
            {
                V1HttpRoute httpRoute => httpRoute.Name(),
                V1GrpcRoute grpcRoute => grpcRoute.Name(),
                V1Ingress v1Ingress => v1Ingress.Name(),
                _ => throw new InvalidOperationException($"GetResourceName: Unexpected type of resource {resource.GetType().Name}")
            };
    }
}
