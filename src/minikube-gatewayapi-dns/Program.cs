using k8s.GatewayApi.Model;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using minikube_gatewayapi_dns;
using minikube_gatewayapi_dns.Configuration;
using minikube_gatewayapi_dns.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(serviceProvider =>
    serviceProvider
        .GetRequiredService<IConfiguration>()
        .BindConfig(new AppConfig()));

builder.Services.AddSingleton<ConcurrentMasterFile>();

builder.Services.AddHostedService<ResourceChangesWatcher<V1HttpRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1GrpcRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1Ingress>>();
builder.Services.AddHostedService<DnsServerWorker>();

builder.Services.AddLogging();

IHost host = builder.Build();

await host.RunAsync();
