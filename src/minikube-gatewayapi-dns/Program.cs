using DNS.Server;
using k8s.GatewayApi.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using minikube_gatewayapi_dns;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<MasterFile>();

builder.Services.AddHostedService<GatewayApiChangesWatcher<HttpRoute>>();
builder.Services.AddHostedService<GatewayApiChangesWatcher<GrpcRoute>>();
builder.Services.AddHostedService<DnsServerWorker>();

builder.Services.AddLogging();

IHost host = builder.Build();

await host.RunAsync();
