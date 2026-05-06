using System.Net;
using System.Net.Sockets;
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
        // Sanity cap for the 2-byte length-prefixed TCP DNS message. EDNS0's well-known
        // buffer size is 4096 (RFC 6891), and our largest legitimate response is dozens of
        // bytes. A misbehaving or malicious peer could otherwise advertise up to 65535
        // and tie up that buffer per connection.
        private const int MaxTcpMessageLength = 4096;

        private readonly AppConfig _config;
        private readonly ConcurrentMasterFile _masterFile;
        private readonly ILogger<DnsServerWorker> _logger;
        private readonly DnsServer _server;

        public DnsServerWorker(AppConfig config, ConcurrentMasterFile masterFile, ILogger<DnsServerWorker> logger)
        {
            _config = config;
            _masterFile = masterFile;
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
            _logger.LogInformation("Starting DNS Server...");

            var podIp = Environment.GetEnvironmentVariable("POD_IP") ?? "0.0.0.0";
            var bindAddress = IPAddress.Parse(podIp);

            _logger.LogInformation("DNS Server listening on {BindAddress}:{Port} (UDP and TCP)...",
                bindAddress, _config.DnsPort);

            var udpTask = _server.Listen(_config.DnsPort, bindAddress);
            var tcpTask = ListenTcpAsync(bindAddress, _config.DnsPort, stoppingToken);

            await Task.WhenAll(udpTask, tcpTask).ConfigureAwait(false);

            _logger.LogInformation("DNS Server shutting down...");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Notified of host shutdown");

            _server.Dispose();

            return base.StopAsync(cancellationToken);
        }

        private async Task ListenTcpAsync(IPAddress bindAddress, int port, CancellationToken stoppingToken)
        {
            var listener = new TcpListener(bindAddress, port);
            listener.Start();
            _logger.LogInformation("TCP DNS listener started on {BindAddress}:{Port}", bindAddress, port);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("TCP accept failed: {Message}", ex.Message);
                        continue;
                    }

                    _ = HandleTcpConnectionAsync(client, stoppingToken);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleTcpConnectionAsync(TcpClient client, CancellationToken stoppingToken)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var lenBuf = new byte[2];
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        // RFC 1035 §4.2.2: TCP messages are prefixed with a 2-byte length field in network byte order.
                        try
                        {
                            await stream.ReadExactlyAsync(lenBuf, stoppingToken).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            // Peer closed the connection cleanly between queries — RFC 7766 allows this.
                            break;
                        }

                        int messageLength = (lenBuf[0] << 8) | lenBuf[1];
                        if (messageLength == 0)
                            continue;
                        if (messageLength > MaxTcpMessageLength)
                        {
                            _logger.LogWarning(
                                "TCP DNS message length {Length} exceeds max {Max}; closing connection",
                                messageLength, MaxTcpMessageLength);
                            break;
                        }

                        var messageBuf = new byte[messageLength];
                        await stream.ReadExactlyAsync(messageBuf, stoppingToken).ConfigureAwait(false);

                        var request = Request.FromArray(messageBuf);
                        var response = await _masterFile.Resolve(request, stoppingToken).ConfigureAwait(false);
                        var responseBytes = response.ToArray();

                        var outLen = new byte[]
                        {
                            (byte)(responseBytes.Length >> 8),
                            (byte)(responseBytes.Length & 0xff)
                        };
                        await stream.WriteAsync(outLen, stoppingToken).ConfigureAwait(false);
                        await stream.WriteAsync(responseBytes, stoppingToken).ConfigureAwait(false);

                        _logger.LogTrace("TCP {Request} => {Response}", request, response);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                _logger.LogWarning("TCP DNS query handling failed: {Message}", ex.Message);
            }
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
