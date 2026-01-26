using DNS.Server;
using k8s;
using k8s.GatewayApi.Model;
using k8s.GatewayApi.Model.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace minikube_gatewayapi_dns
{
    internal class GatewayApiChangesWatcher<TCustomResource> : BackgroundService
        where TCustomResource : IKubernetesObject
    {
        private readonly MasterFile _masterFile;
        private readonly ILogger<GatewayApiChangesWatcher<TCustomResource>> _logger;
        private readonly Kubernetes _client;
        private readonly GenericClient _typedClient;

        private static string GatewaysCrdName = "gateways.gateway.networking.k8s.io";

        public GatewayApiChangesWatcher(MasterFile masterFile, ILogger<GatewayApiChangesWatcher<TCustomResource>> logger)
        {
            _masterFile = masterFile;
            _logger = logger;

            var config = IsRunningInKubePod()
                ? KubernetesClientConfiguration.InClusterConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile();
            _client = new Kubernetes(config);
            _typedClient = new GenericClient(_client, 
                IKubernetesObjectExtensions.GetKubernetesEntityGroup<TCustomResource>(), 
                IKubernetesObjectExtensions.GetKubernetesEntityVersion<TCustomResource>(), 
                IKubernetesObjectExtensions.GetKubernetesEntityPluralName<TCustomResource>());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!await CheckIfCrdExistsAsync(_client, GatewaysCrdName, stoppingToken) && 
                   !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("The GatewayApi CRDs are not installed on this cluster. Checking again in 10 seconds...");

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            _logger.LogInformation($"The GatewayApi CRDs are detected, continuing to monitor {typeof(TCustomResource).Name} for changes...");

            if (stoppingToken.IsCancellationRequested)
                return;

            //Configure watchers for Gateway API resources
            // var resources =
            //     _client.WatchListCustomObjectForAllNamespacesAsync(
            //         IKubernetesObjectExtensions.GetKubernetesEntityGroup<TCustomResource>(), //"gateway.networking.k8s.io",
            //         IKubernetesObjectExtensions.GetKubernetesEntityVersion<TCustomResource>(), //"v1",
            //         IKubernetesObjectExtensions.GetKubernetesEntityPluralName<TCustomResource>(), //"httproutes",
            //         cancellationToken: stoppingToken);

            var resources =
                _typedClient.WatchAsync<TCustomResource>(cancel: stoppingToken);

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

            //
            // // Fetch all objects for the specific route type across all namespaces
            // // Gateway API Group: gateway.networking.k8s.io, Version: v1
            // var httpRoutesResult = await _client.CustomObjects.ListClusterCustomObjectAsync<ListClusterCustomObjectResult<HttpRoute>>(
            //     group: "gateway.networking.k8s.io",
            //     version: "v1",
            //     plural: "httproutes",
            //     cancellationToken: stoppingToken);
            //
            // var grpcRoutesResult = await _client.CustomObjects.ListClusterCustomObjectAsync<ListClusterCustomObjectResult<GrpcRoute>>(
            //     group: "gateway.networking.k8s.io",
            //     version: "v1",
            //     plural: "grpcroutes",
            //     cancellationToken: stoppingToken);
            //
            // var hosts = grpcRoutesResult.Items.SelectMany(grpcRoute => grpcRoute.Spec.Hostnames)
            //     .Concat(httpRoutesResult.Items.SelectMany(httpRoute => httpRoute.Spec.Hostnames))
            //     .Distinct();
            //
            // _logger.LogInformation($"GrpcRoutes: {System.Text.Json.JsonSerializer.Serialize(hosts)}");
            //
            // var dnsServerIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";
            //
            // foreach (var host in hosts)
            // {
            //     _logger.LogInformation($"Creating DNS entry for {host} to point to {dnsServerIp}");
            //     _masterFile.AddIPAddressResourceRecord(host, dnsServerIp);
            // }
        }

        /// <summary>
        /// Checks if a specific Custom Resource Definition (CRD) exists in the Kubernetes cluster.
        /// </summary>
        /// <param name="client">The Kubernetes client instance.</param>
        /// <param name="crdName">The full name of the CRD (e.g., "gateways.gateway.networking.k8s.io").</param>
        /// <param name="stoppingToken"></param>
        /// <returns>True if the CRD exists, false otherwise.</returns>
        public async Task<bool> CheckIfCrdExistsAsync(Kubernetes client, string crdName,
            CancellationToken stoppingToken)
        {
            try
            {
                var crd = await client.ReadCustomResourceDefinitionAsync(crdName, cancellationToken: stoppingToken);
                return crd != null;
            }
            catch (k8s.Autorest.HttpOperationException ex)
            {
                // If the API returns a 404 Not Found status, the CRD does not exist.
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError($"Checking for existence of CRD '{crdName}' resulted in HTTP Status: {ex.Response.StatusCode}");
                    return false;
                }

                // Re-throw other exceptions (e.g., authentication issues, network errors)
                throw;
            }
        }

        private bool IsRunningInKubePod() => 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_PORT"));

        private string[] GetHostnames(object resource) =>
            resource is HttpRoute route
                ? route.Spec.Hostnames.ToArray()
                : resource is GrpcRoute grpcRoute
                    ? grpcRoute.Spec.Hostnames.ToArray()
                    : throw new InvalidOperationException($"Unexpected type of resource {resource.GetType().Name}");
    }
}
