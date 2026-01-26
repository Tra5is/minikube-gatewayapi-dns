using DNS.Server;
using k8s.GatewayApi.Model;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using minikube_gatewayapi_dns;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<MasterFile>();

builder.Services.AddHostedService<ResourceChangesWatcher<HttpRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<GrpcRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1Ingress>>();
builder.Services.AddHostedService<DnsServerWorker>();

builder.Services.AddLogging();

IHost host = builder.Build();

await host.RunAsync();
