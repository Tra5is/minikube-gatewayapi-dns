using Microsoft.Extensions.Configuration;

namespace minikube_gatewayapi_dns.Extensions;

public static class ConfigurationExtensions
{
    public static T BindConfig<T>(this IConfiguration configuration, T config)
    {
        configuration.Bind(config);
        return config;
    }
}