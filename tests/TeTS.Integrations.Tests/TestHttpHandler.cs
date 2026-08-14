using System.Net;
using System.Text;

namespace TeTS.Integrations.Tests;

/// <summary>Canned-response HttpMessageHandler that records every outgoing request (with body).</summary>
public sealed class TestHttpHandler : HttpMessageHandler
{
    public sealed record Recorded(HttpRequestMessage Request, string? Body);
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    public List<Recorded> Requests { get; } = new();

    public TestHttpHandler Enqueue(HttpStatusCode status, string body, Action<HttpResponseMessage>? mutate = null)
    {
        _responses.Enqueue(_ =>
        {
            var resp = new HttpResponseMessage(status)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            mutate?.Invoke(resp);
            return resp;
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add(new Recorded(request, body));
        if (_responses.Count == 0) throw new InvalidOperationException("TestHttpHandler: no response enqueued.");
        return _responses.Dequeue()(request);
    }
}
