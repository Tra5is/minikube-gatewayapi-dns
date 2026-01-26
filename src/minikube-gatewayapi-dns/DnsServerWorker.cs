using System.Net;
using DNS.Client;
using DNS.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace minikube_gatewayapi_dns
{
    internal class DnsServerWorker : BackgroundService
    {
        private readonly ILogger<DnsServerWorker> _logger;
        private readonly DnsServer _server;

        public DnsServerWorker(MasterFile masterFile, ILogger<DnsServerWorker> logger)
        {
            _logger = logger;

            _server = new DnsServer(masterFile);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _server.Responded += OnServerOnResponded;
            _server.Listening += OnServerOnListening;
            _server.Errored += OnServerOnErrored;

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Starting DNS Server...");

            //TODO: remove hardcoded port
            var port = 53;
            var podIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";

            _logger.LogInformation($"DNS Server listening to {podIp} on port {port}...");

            await _server.Listen(port, IPAddress.Parse(podIp)).ConfigureAwait(false);
            
            _logger.LogInformation("DNS Server shutting down...");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Notified of host shutdown");
            
            _server.Dispose();
            
            return base.StopAsync(cancellationToken);
        }

        private void OnServerOnErrored(object? sender, DnsServer.ErroredEventArgs e)
        {
            _logger.LogError(e.Exception, "Errored: {0}", e);
            if (e.Exception is ResponseException responseError) 
                _logger.LogError(e.Exception, "Response Error: {0}", responseError.Response);
        }

        private void OnServerOnListening(object? sender, EventArgs e) => 
            _logger.LogInformation("DNS Server Listening");

        private void OnServerOnResponded(object? sender, DnsServer.RespondedEventArgs e) => 
            _logger.LogTrace("{0} => {1}", e.Request, e.Response);
    }
}
