using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Extensions.Rdf;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：<see cref="OdfRdfGraphBridge.ExecuteQuery"/> 不得因查詢中的 <c>SERVICE</c> 子句而對外發出網路要求。
/// 修正前，<c>SERVICE &lt;http://127.0.0.1:port/…&gt;</c> 會讓 dotNetRDF 查詢引擎實際送出 HTTP GET
/// （以本機 <see cref="HttpListener"/> 實測）；若 SPARQL 字串來自不受信任的使用者，就是 SSRF。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class SparqlServiceClauseTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _prefix;
    private int _hits;

    public SparqlServiceClauseTests()
    {
        // 由系統挑選可用的連接埠，避免與其他測試衝突。
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _prefix = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_prefix);
        _listener.Start();
        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    Interlocked.Increment(ref _hits);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/sparql-results+xml";
                    byte[] body = Encoding.UTF8.GetBytes(
                        "<?xml version=\"1.0\"?><sparql xmlns=\"http://www.w3.org/2005/sparql-results#\"><head><variable name=\"s\"/></head><results/></sparql>");
                    await context.Response.OutputStream.WriteAsync(body);
                    context.Response.Close();
                }
                catch
                {
                    break;
                }
            }
        });
    }

    public void Dispose()
    {
        _listener.Close();
    }

    private static object Query(string sparql) => OdfRdfGraphBridge.ExecuteQuery(new OdfRdfMetadata(), sparql);

    [Fact]
    public async Task ServiceClauseIsRejectedAndSendsNoNetworkRequest()
    {
        string query = $"SELECT * WHERE {{ SERVICE <{_prefix}sparql> {{ ?s ?p ?o }} }}";

        Assert.Throws<ArgumentException>(() => Query(query));

        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref _hits));
    }

    [Theory]
    [InlineData("SELECT * WHERE {{ service <{0}sparql> {{ ?s ?p ?o }} }}")]
    [InlineData("SELECT * WHERE {{ SERVICE SILENT <{0}sparql> {{ ?s ?p ?o }} }}")]
    [InlineData("SELECT * WHERE {{ OPTIONAL {{ SERVICE <{0}sparql> {{ ?s ?p ?o }} }} }}")]
    [InlineData("SELECT * WHERE {{ {{ ?s ?p ?o }} UNION {{ SERVICE <{0}sparql> {{ ?s ?p ?o }} }} }}")]
    [InlineData("SELECT * WHERE {{ {{ SELECT * WHERE {{ SERVICE <{0}sparql> {{ ?s ?p ?o }} }} }} }}")]
    [InlineData("# comment first\nSELECT * WHERE {{\nSERVICE<{0}sparql>{{ ?s ?p ?o }}\n}}")]
    [InlineData("SELECT * WHERE {{ FILTER EXISTS {{ SERVICE <{0}sparql> {{ ?s ?p ?o }} }} }}")]
    // 省略空白的寫法：自寫掃描器與剖析器的切詞可能不同，因此另以剖析後的正規化查詢再掃描一次。
    [InlineData("SELECT * WHERE {{ ?s ?p <http://a/b>.SERVICE <{0}sparql> {{ ?a ?b ?c }} }}")]
    [InlineData("SELECT * WHERE {{ ?s ?p ?o.SERVICE <{0}sparql> {{ ?a ?b ?c }} }}")]
    [InlineData("SELECT * WHERE {{ ?s ?p \"lit\".SERVICE <{0}sparql> {{ ?a ?b ?c }} }}")]
    public async Task ServiceClauseInAnyPositionIsRejected(string template)
    {
        string query = string.Format(CultureInfo.InvariantCulture, template, _prefix);

        Assert.Throws<ArgumentException>(() => Query(query));

        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref _hits));
    }

    [Theory]
    [InlineData("SELECT * WHERE { ?s ?p ?o FILTER(CONTAINS(STR(?o), \"service\")) }")]
    [InlineData("SELECT * WHERE { ?s ?p \"SERVICE <http://x/y> { ?a ?b ?c }\" }")]
    [InlineData("SELECT * WHERE { ?s ?p '''SERVICE ''' }")]
    [InlineData("PREFIX service: <http://example.org/service/> SELECT * WHERE { ?s service:name ?o }")]
    [InlineData("SELECT * WHERE { ?s <http://example.org/SERVICE> ?o }")]
    [InlineData("SELECT ?service WHERE { ?service ?p ?o }")]
    [InlineData("SELECT * WHERE { ?s ?p ?o } # SERVICE <http://x/y> { }")]
    [InlineData("ASK { ?s ?p ?o }")]
    public void QueriesMentioningServiceOnlyInsideLiteralsIrisOrNamesStillRun(string query)
    {
        object result = Query(query);

        Assert.NotNull(result);
    }
}
