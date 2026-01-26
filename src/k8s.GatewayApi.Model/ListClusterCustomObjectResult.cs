using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace k8s.GatewayApi.Model
{
    public class ListClusterCustomObjectResult<TResult> : KubernetesListResult<TResult>
    {
    }

    public class KubernetesListResult<T>
    {
        [JsonPropertyName("apiVersion")]
        public string ApiVersion { get; set; }

        [JsonPropertyName("items")]
        public List<T> Items { get; set; }
    }
}
