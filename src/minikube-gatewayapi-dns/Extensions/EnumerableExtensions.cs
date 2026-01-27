using System.Diagnostics.CodeAnalysis;

namespace minikube_gatewayapi_dns.Extensions;

//Extensions for fluent calls
public static class EnumerableExtensions
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
        return source;
    }
}