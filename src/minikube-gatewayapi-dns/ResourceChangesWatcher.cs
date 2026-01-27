using DNS.Server;
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
        private readonly MasterFile _masterFile;
        private readonly ILogger<ResourceChangesWatcher<TResource>> _logger;
        private readonly Kubernetes _client;
        private readonly GenericClient _typedClient;

        private static string GatewaysCrdName = "gateways.gateway.networking.k8s.io";

        public ResourceChangesWatcher(MasterFile masterFile, ILogger<ResourceChangesWatcher<TResource>> logger)
        {
            _masterFile = masterFile;
            _logger = logger;

            var config = IsRunningInKubePod()
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile();
            _client = new Kubernetes(config);
            _typedClient = new GenericClient(_client, 
                IKubernetesObjectExtensions.GetKubernetesEntityGroup<TResource>(), 
                IKubernetesObjectExtensions.GetKubernetesEntityVersion<TResource>(), 
                IKubernetesObjectExtensions.GetKubernetesEntityPluralName<TResource>());
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

                if (watchEventType == WatchEventType.Added || watchEventType == WatchEventType.Modified)
                {
                    var hostnames = GetHostnames(resource);
                    var dnsServerIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";

                    foreach(var host in hostnames)
                    {
                        _logger.LogInformation($"Creating DNS entry for {host} to point to {dnsServerIp}");
                        _masterFile.AddIPAddressResourceRecord(host, dnsServerIp);
                    }
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

        private bool IsRunningInKubePod() => 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_PORT"));

        private string[] GetHostnames(object resource) =>
            resource switch
            {
                HttpRoute route => route.Spec.Hostnames.ToArray(),
                GrpcRoute grpcRoute => grpcRoute.Spec.Hostnames.ToArray(),
                V1Ingress v1Ingress => v1Ingress.Spec.Rules.Select(rule => rule.Host).ToArray(),
                _ => throw new InvalidOperationException($"GetHostnames: Unexpected type of resource {resource.GetType().Name}")
            };
    }
}
