using k8s.GatewayApi.Model;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using minikube_gatewayapi_dns;
using minikube_gatewayapi_dns.Configuration;
using minikube_gatewayapi_dns.Extensions;
using minikube_gatewayapi_dns.Resolution;
using minikube_gatewayapi_dns.Watchers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(serviceProvider =>
    serviceProvider
        .GetRequiredService<IConfiguration>()
        .BindConfig(new AppConfig()));

builder.Services.AddSingleton<ConcurrentMasterFile>();
builder.Services.AddSingleton<GatewayCache>();
builder.Services.AddSingleton<IUpstreamResolver, SystemUpstreamResolver>();

builder.Services.AddSingleton<IAddressResolver<V1Ingress>, IngressAddressResolver>();
builder.Services.AddSingleton<IAddressResolver<V1HttpRoute>, RouteAddressResolver<V1HttpRoute>>();
builder.Services.AddSingleton<IAddressResolver<V1GrpcRoute>, RouteAddressResolver<V1GrpcRoute>>();

var fallbackIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";

builder.Services.AddSingleton<IWatchEventHandler<V1Ingress>>(sp =>
    new IngressDnsRecordEventHandler(
        sp.GetRequiredService<ConcurrentMasterFile>(),
        sp.GetRequiredService<IAddressResolver<V1Ingress>>(),
        fallbackIp,
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IngressDnsRecordEventHandler>>()));

builder.Services.AddSingleton<IWatchEventHandler<V1HttpRoute>, RouteDnsRecordEventHandler<V1HttpRoute>>();
builder.Services.AddSingleton<IWatchEventHandler<V1GrpcRoute>, RouteDnsRecordEventHandler<V1GrpcRoute>>();
builder.Services.AddSingleton<IWatchEventHandler<V1Gateway>, GatewayCacheEventHandler>();

builder.Services.AddHostedService<ResourceChangesWatcher<V1Gateway>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1HttpRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1GrpcRoute>>();
builder.Services.AddHostedService<ResourceChangesWatcher<V1Ingress>>();
builder.Services.AddHostedService<DnsServerWorker>();

builder.Services.AddLogging();

IHost host = builder.Build();

await host.RunAsync();
