using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace minikube_gatewayapi_dns.Tests;

public class ConcurrentMasterFileTests
{
    private static ConcurrentMasterFile NewFile() => new(NullLogger<ConcurrentMasterFile>.Instance);

    [Fact]
    public async Task Returns_All_Addresses_For_Same_Hostname_From_Same_Resource()
    {
        var file = NewFile();
        Assert.True(file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1"));
        Assert.True(file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.2"));

        var request = new Request();
        request.Questions.Add(new Question(new Domain("foo.example.com"), RecordType.A));
        var response = await file.Resolve(request);

        var ips = response.AnswerRecords
            .OfType<IPAddressResourceRecord>()
            .Select(r => r.IPAddress.ToString())
            .OrderBy(s => s)
            .ToArray();

        Assert.Equal(new[] { "10.0.0.1", "10.0.0.2" }, ips);
    }

    [Fact]
    public async Task Removing_By_Resource_Removes_All_Of_Its_Addresses()
    {
        var file = NewFile();
        file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1");
        file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.2");

        file.RemoveIPAddressResourceRecord("res-1");

        var request = new Request();
        request.Questions.Add(new Question(new Domain("foo.example.com"), RecordType.A));
        var response = await file.Resolve(request);
        Assert.Empty(response.AnswerRecords);
    }

    [Fact]
    public async Task Wildcard_Matching_Still_Works()
    {
        var file = NewFile();
        Assert.True(file.AddIPAddressResourceRecord("res-1", "*.example.com", "10.0.0.1"));

        var request = new Request();
        request.Questions.Add(new Question(new Domain("anything.example.com"), RecordType.A));
        var response = await file.Resolve(request);

        var ip = Assert.IsType<IPAddressResourceRecord>(Assert.Single(response.AnswerRecords));
        Assert.Equal("10.0.0.1", ip.IPAddress.ToString());
    }

    [Fact]
    public void Idempotent_Add_Returns_False_On_Replay_But_Does_Not_Throw()
    {
        var file = NewFile();
        Assert.True(file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1"));
        Assert.False(file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1"));
    }

    [Fact]
    public async Task Returns_NoError_For_AAAA_When_Only_A_Records_Exist()
    {
        // RFC 2308 §2.2: NODATA, not NXDOMAIN, when the name exists but the requested
        // type doesn't. Returning NXDOMAIN poisons resolver negative caches for the
        // whole name (e.g. Windows DNS Client suppresses subsequent A queries).
        var file = NewFile();
        file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1");

        var request = new Request();
        request.Questions.Add(new Question(new Domain("foo.example.com"), RecordType.AAAA));
        var response = await file.Resolve(request);

        Assert.Equal(ResponseCode.NoError, response.ResponseCode);
        Assert.Empty(response.AnswerRecords);
    }

    [Fact]
    public async Task Returns_NameError_For_Unknown_Hostname()
    {
        var file = NewFile();
        file.AddIPAddressResourceRecord("res-1", "foo.example.com", "10.0.0.1");

        var request = new Request();
        request.Questions.Add(new Question(new Domain("nope.example.com"), RecordType.A));
        var response = await file.Resolve(request);

        Assert.Equal(ResponseCode.NameError, response.ResponseCode);
        Assert.Empty(response.AnswerRecords);
    }
}
