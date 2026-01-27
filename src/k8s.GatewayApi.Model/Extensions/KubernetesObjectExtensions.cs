using System.Reflection;
using k8s.Models;

namespace k8s.GatewayApi.Model.Extensions;

public static class KubernetesObjectExtensions
{
    public static KubernetesEntityAttribute? GetKubernetesEntityAttribute<T>() where T : IKubernetesObject => 
        typeof(T).GetCustomAttribute<KubernetesEntityAttribute>();

    public static string GetKubernetesEntityGroup<T>() where T : IKubernetesObject => 
        GetKubernetesEntityAttribute<T>()?.Group ?? string.Empty;

    public static string GetKubernetesEntityVersion<T>() where T : IKubernetesObject =>
        GetKubernetesEntityAttribute<T>()?.ApiVersion ?? string.Empty;

    public static string GetKubernetesEntityPluralName<T>() where T : IKubernetesObject =>
        GetKubernetesEntityAttribute<T>()?.PluralName ?? string.Empty;
}