using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Redactor.Redactor;

/// <summary>
/// Maintains a set of long-lived HTTP responses configured for Server-Sent
/// Events and broadcasts JSON messages to all subscribers.
/// </summary>
internal sealed class EventStreamService
{
    private readonly ConcurrentDictionary<Guid, HttpListenerResponse> _subscribers = new();
    private readonly Timer _heartbeat;

    public EventStreamService()
    {
        _heartbeat = new Timer(_ => Broadcast(new { type = "ping" }),
            null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Attaches the supplied response as an SSE subscriber. The returned task
    /// completes when the connection is closed or torn down by the client.
    /// </summary>
    public async Task SubscribeAsync(HttpListenerResponse res, CancellationToken ct)
    {
        res.StatusCode = 200;
        res.ContentType = "text/event-stream";
        res.AddHeader("Cache-Control", "no-cache");
        res.AddHeader("Connection", "keep-alive");
        res.SendChunked = true;

        var id = Guid.NewGuid();
        _subscribers[id] = res;

        try
        {
            // Send an initial "ready" event so the client knows the channel is live.
            await SendAsync(res, new { type = "ready" });
            // Block while the connection is held open. The response is closed
            // either by Dispose (server shutdown) or by a write failure.
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (TaskCanceledException) { /* expected on shutdown */ }
        catch (Exception) { /* connection torn down */ }
        finally
        {
            _subscribers.TryRemove(id, out _);
            try { res.Close(); } catch { /* already closed */ }
        }
    }

    public void Broadcast(object payload)
    {
        if (_subscribers.IsEmpty) return;
        foreach (var (id, res) in _subscribers)
        {
            _ = SendAsync(res, payload).ContinueWith(t =>
            {
                if (t.IsFaulted) _subscribers.TryRemove(id, out _);
            }, TaskScheduler.Default);
        }
    }

    private static async Task SendAsync(HttpListenerResponse res, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
        await res.OutputStream.WriteAsync(bytes);
        await res.OutputStream.FlushAsync();
    }
}
