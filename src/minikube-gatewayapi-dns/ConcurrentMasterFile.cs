using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using Microsoft.Extensions.Logging;
using minikube_gatewayapi_dns.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

// ReSharper disable InconsistentNaming

namespace minikube_gatewayapi_dns;

internal class ConcurrentMasterFile : IRequestResolver
{
    private record ResourceRecordKey(string ResourceId, Domain Domain, RecordType Type);

    protected static readonly TimeSpan DEFAULT_TTL = new TimeSpan(0L);

    private readonly ILogger<ConcurrentMasterFile> logger;
    private readonly ConcurrentDictionary<ResourceRecordKey, IResourceRecord> threadSafeEntries = new();

    public ConcurrentMasterFile(ILogger<ConcurrentMasterFile> logger)
    {
        this.logger = logger;
    }

    public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = new())
    {
        IResponse result = Response.FromRequest(request);
        foreach (Question question in request.Questions)
        {
            var l2 = this.Get(question);
            if (l2.Count > 0)
                foreach(var answer in l2)
                    result.AnswerRecords.Add(answer);
            else
                result.ResponseCode = ResponseCode.NameError;
        }
        return Task.FromResult(result);
    }

    public bool AddIPAddressResourceRecord(string resourceId, string domain, string ipAddress) =>
        AddIPAddressResourceRecord(resourceId, new Domain(domain), IPAddress.Parse(ipAddress));

    public bool AddIPAddressResourceRecord(string resourceId, Domain domain, IPAddress ipAddress) =>
        threadSafeEntries.TryAdd(
            new ResourceRecordKey(resourceId, domain, RecordType.A),
            new IPAddressResourceRecord(domain, ipAddress, DEFAULT_TTL));

    // ReSharper disable once InconsistentNaming

    public void RemoveIPAddressResourceRecord(string resourceId) =>
        threadSafeEntries.Keys
            .Where(IsRecordForResource(resourceId))
            .Where(IsIPAddressRecord)
            .Select(TryRemoveResourceRecord)
            .Do(LogRemovals);

    private static Func<ResourceRecordKey, bool> IsRecordForResource(string resourceId) => resourceRecordKey =>
        resourceRecordKey.ResourceId == resourceId;

    private static bool IsIPAddressRecord(ResourceRecordKey resourceRecordKey) =>
        resourceRecordKey.Type == RecordType.A;

    private (ResourceRecordKey, bool) TryRemoveResourceRecord(ResourceRecordKey resourceRecordKey) =>
        (resourceRecordKey, threadSafeEntries.TryRemove(resourceRecordKey, out _));

    private void LogRemovals((ResourceRecordKey resourceRecordKey, bool result) arg)
    {
        if (arg.result)
            logger.LogTrace(
                $"Removal of {arg.resourceRecordKey.Domain} for resource {arg.resourceRecordKey.ResourceId} succeeded");
        else
            logger.LogWarning(
                $"Removal of {arg.resourceRecordKey.Domain} for resource {arg.resourceRecordKey.ResourceId} failed");
    }

    private IList<IResourceRecord> Get(Domain domain, RecordType type) =>
        threadSafeEntries.Values
            .Where(IsMatchingRecord(domain, type))
            .ToList();

    private IList<IResourceRecord> Get(Question question) => 
        this.Get(question.Name, question.Type);

    private static Func<IResourceRecord, bool> IsMatchingRecord(Domain domain, RecordType type) =>
        e => 
            Matches(domain, e.Name) &&
            (type == RecordType.ANY || e.Type == type);

    private static bool Matches(Domain domain, Domain entry)
    {
        var regexStr = entry.ToString()
            .Split('.')
            .Select(EscapeAndMatchWildcard)
            .Join("\\.");
        return new Regex($"^{regexStr}$", RegexOptions.IgnoreCase).IsMatch(domain.ToString());
    }

    private static string EscapeAndMatchWildcard(string strPart) =>
        strPart == "*" ? "(\\w+)" : Regex.Escape(strPart);
}