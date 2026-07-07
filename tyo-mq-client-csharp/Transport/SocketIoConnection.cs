using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace TyoMq.Transport;

/// <summary>
/// One Socket.IO v4 connection over a BCL ClientWebSocket: Engine.IO
/// handshake, ping→pong keepalive, thread-safe event emit, and event
/// dispatch from a background receive loop.
/// </summary>
public sealed class SocketIoConnection : IDisposable
{
    private const int MaxFrameBytes = 4 * 1024 * 1024;

    private readonly Uri uri;
    private readonly ClientWebSocket ws = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly object handlerLock = new();
    private readonly Dictionary<string, List<Action<JsonNode?>>> handlers = new();
    private Task? receiveLoop;
    private volatile bool connected;

    public event Action? Connected;
    public event Action? Disconnected;
    public bool IsConnected => connected;

    public SocketIoConnection(string url)
    {
        var u = new Uri(url);
        var scheme = u.Scheme switch
        {
            "https" or "wss" => "wss",
            _ => "ws",
        };
        uri = new Uri($"{scheme}://{u.Host}:{u.Port}/socket.io/?EIO=4&transport=websocket");
    }

    /// <summary>Connects and completes once the Socket.IO session is open.</summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await ws.ConnectAsync(uri, ct).ConfigureAwait(false);

        var open = SioCodec.Decode(await ReceiveFrameAsync(ct).ConfigureAwait(false));
        if (open.Kind != SioMessageKind.Open)
            throw new IOException($"expected Engine.IO open, got kind {open.Kind}");

        await SendFrameAsync(SioCodec.SioConnect, ct).ConfigureAwait(false);
        var sio = SioCodec.Decode(await ReceiveFrameAsync(ct).ConfigureAwait(false));
        if (sio.Kind != SioMessageKind.SioConnected)
            throw new IOException($"expected Socket.IO connected, got kind {sio.Kind}");

        connected = true;
        Connected?.Invoke();
        receiveLoop = Task.Run(ReceiveLoopAsync, CancellationToken.None);
    }

    /// <summary>Registers a handler for a Socket.IO event (additive).</summary>
    public void On(string eventName, Action<JsonNode?> handler)
    {
        lock (handlerLock)
        {
            if (!handlers.TryGetValue(eventName, out var list))
                handlers[eventName] = list = new List<Action<JsonNode?>>();
            list.Add(handler);
        }
    }

    /// <summary>Removes all handlers for an event.</summary>
    public void Off(string eventName)
    {
        lock (handlerLock)
            handlers.Remove(eventName);
    }

    public void Emit(string eventName, JsonNode? payload) =>
        EmitAsync(eventName, payload).GetAwaiter().GetResult();

    public async Task EmitAsync(string eventName, JsonNode? payload, CancellationToken ct = default) =>
        await SendFrameAsync(SioCodec.EncodeEvent(eventName, payload), ct).ConfigureAwait(false);

    public void Disconnect()
    {
        connected = false;
        try
        {
            if (ws.State == WebSocketState.Open)
                ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                  .GetAwaiter().GetResult();
        }
        catch
        {
            // closing a dying socket is best-effort
        }
    }

    public void Dispose()
    {
        Disconnect();
        ws.Dispose();
    }

    private async Task SendFrameAsync(string frame, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        await sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task<string> ReceiveFrameAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        for (;;)
        {
            var result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException("websocket closed by server");
            ms.Write(buffer, 0, result.Count);
            if (ms.Length > MaxFrameBytes)
                throw new IOException("frame exceeds size limit");
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var msg = SioCodec.Decode(await ReceiveFrameAsync(CancellationToken.None).ConfigureAwait(false));
                switch (msg.Kind)
                {
                case SioMessageKind.Ping:
                    await SendFrameAsync(SioCodec.EnginePong, CancellationToken.None).ConfigureAwait(false);
                    break;
                case SioMessageKind.Event:
                    Dispatch(msg.EventName!, msg.Payload);
                    break;
                default:
                    break; // open/connected duplicates and unknown frames are ignored
                }
            }
        }
        catch
        {
            // fall through to disconnected state; receive errors are terminal
        }
        if (connected)
        {
            connected = false;
            Disconnected?.Invoke();
        }
    }

    private void Dispatch(string eventName, JsonNode? payload)
    {
        Action<JsonNode?>[] snapshot;
        lock (handlerLock)
        {
            if (!handlers.TryGetValue(eventName, out var list))
                return;
            snapshot = list.ToArray();
        }
        foreach (var h in snapshot)
        {
            try
            {
                h(payload);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"tyo-mq: handler for {eventName} failed: {e.Message}");
            }
        }
    }
}
