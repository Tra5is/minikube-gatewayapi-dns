using System.Net;
using DNS.Client;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using minikube_gatewayapi_dns.Configuration;

namespace minikube_gatewayapi_dns
{
    internal class DnsServerWorker : BackgroundService
    {
        private readonly AppConfig _config;
        private readonly ILogger<DnsServerWorker> _logger;
        private readonly DnsServer _server;

        public DnsServerWorker(AppConfig config, ConcurrentMasterFile masterFile, ILogger<DnsServerWorker> logger)
        {
            _config = config;
            _logger = logger;

            _server = new DnsServer(masterFile);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _server.Requested += OnServerRequested;
            _server.Responded += OnServerResponded;
            _server.Listening += OnServerListening;
            _server.Errored += OnServerErrored;

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Starting DNS Server...");

            var podIp = Environment.GetEnvironmentVariable("POD_IP") ?? "0.0.0.0";

            _logger.LogInformation($"DNS Server listening to {podIp} on port {_config.DnsPort}...");

            await _server.Listen(_config.DnsPort, IPAddress.Parse(podIp)).ConfigureAwait(false);
            
            _logger.LogInformation("DNS Server shutting down...");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Notified of host shutdown");
            
            _server.Dispose();
            
            return base.StopAsync(cancellationToken);
        }

        private void OnServerRequested(object? sender, DnsServer.RequestedEventArgs e) =>
            _logger.LogTrace("Requested {0}", e);

        private void OnServerErrored(object? sender, DnsServer.ErroredEventArgs e)
        {
            _logger.LogError(e.Exception, "Errored: {0}", e);
            if (e.Exception is ResponseException responseError) 
                _logger.LogError(e.Exception, "Response Error: {0}", responseError.Response);
        }

        private void OnServerListening(object? sender, EventArgs e) => 
            _logger.LogInformation("DNS Server Listening");

        private void OnServerResponded(object? sender, DnsServer.RespondedEventArgs e) => 
            _logger.LogTrace("{0} => {1}", e.Request, e.Response);
    }

    internal class NoneResolver : IRequestResolver
    {
        public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken) => 
            Task.FromResult((IResponse)Response.FromRequest(request));
    }
}
