using System.Text.Json.Nodes;
using TyoMq.Transport;

namespace TyoMq;

/// <summary>
/// A tyo-mq connection. One Client can act as a producer, a consumer, or
/// both; create one per logical service identity. Feature parity with the
/// Node/Python/Go/Rust/Ruby/C++/Java clients: structured JSON payloads,
/// durable delivery with ACK/retry/dead-lettering, MQTT-style topic
/// wildcards, consumer groups, and token authentication.
/// </summary>
public sealed class Client : IDisposable
{
    public const int DefaultPort = 17352;

    /// <summary>Subscribes to an event (or topic pattern) from any producer.</summary>
    public const string AllProducers = "TYO-MQ-ALL";

    private const string EventAll = "TM-ALL";
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly SocketIoConnection conn;
    private readonly Chunking chunking;

    public event Action? Connected
    {
        add => conn.Connected += value;
        remove => conn.Connected -= value;
    }

    public event Action? Disconnected
    {
        add => conn.Disconnected += value;
        remove => conn.Disconnected -= value;
    }

    public bool IsConnected => conn.IsConnected;

    public Client(string url)
    {
        conn = new SocketIoConnection(url);
        chunking = new Chunking(conn);
    }

    public Client(string host, int port = DefaultPort, string protocol = "http")
        : this($"{protocol}://{host}:{port}")
    {
    }

    /// <summary>The Socket.IO event on which deliveries for a subscription arrive.</summary>
    public static string ConsumeEventName(string producer, string @event, string scope = "")
    {
        if (scope == "all")
            return "CONSUME-" + producer.ToLowerInvariant() + "-" + EventAll;
        return "CONSUME-" + (producer + "-" + @event).ToLowerInvariant();
    }

    public Task ConnectAsync(CancellationToken ct = default) => conn.ConnectAsync(ct);

    /// <summary>
    /// Sends AUTHENTICATION and completes on AUTH_OK; throws on AUTH_FAIL or
    /// timeout. Call right after ConnectAsync when the server has auth enabled.
    /// </summary>
    public async Task AuthenticateAsync(string token, CancellationToken ct = default)
    {
        var outcome = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On("AUTH_OK", _ => outcome.TrySetResult(null));
        conn.On("AUTH_FAIL", p => outcome.TrySetResult(p?.ToJsonString() ?? "authentication failed"));
        await conn.EmitAsync("AUTHENTICATION", new JsonObject { ["token"] = token }, ct).ConfigureAwait(false);

        var done = await Task.WhenAny(outcome.Task, Task.Delay(HandshakeTimeout, ct)).ConfigureAwait(false);
        if (done != outcome.Task)
            throw new TimeoutException("timed out waiting for AUTH_OK/AUTH_FAIL");
        if (outcome.Task.Result is string failure)
            throw new UnauthorizedAccessException($"authentication failed: {failure}");
    }

    /// <summary>Announces this connection as a producer named <paramref name="name"/>.</summary>
    public Task RegisterProducerAsync(string name, CancellationToken ct = default) =>
        conn.EmitAsync("PRODUCER", new JsonObject { ["name"] = name }, ct);

    /// <summary>
    /// Announces this connection as a consumer. The name doubles as the
    /// durable consumer identity: reconnect with the same name to replay
    /// queued messages of a durable subscription.
    /// </summary>
    public Task RegisterConsumerAsync(string name, CancellationToken ct = default) =>
        conn.EmitAsync("CONSUMER", new JsonObject
        {
            ["name"] = name,
            ["id"] = name,
            ["consumer_id"] = name,
        }, ct);

    /// <summary>Publishes one fire-and-forget message; payloads larger than
    /// the chunk threshold are split into PRODUCE_CHUNK frames.</summary>
    public void Produce(string producer, string @event, JsonNode? payload) =>
        chunking.Produce(producer, @event, payload);

    /// <summary>Acknowledges one ACK-enabled delivery by its msgId.</summary>
    public void Ack(string msgId) =>
        conn.Emit("ACK", new JsonObject { ["msgId"] = msgId });

    /// <summary>
    /// Sends a SUBSCRIBE request and dispatches matching deliveries to
    /// <paramref name="handler"/>. With options.Ack and not ManualAck,
    /// deliveries are acknowledged automatically after the handler returns
    /// without throwing.
    /// </summary>
    public void Subscribe(SubscribeOptions options, ConsumeHandler handler)
    {
        var payload = BuildSubscribePayload(options);
        var producer = (string)payload["producer"]!;
        var autoAck = (options.Ack || options.ManualAck) && !options.ManualAck;

        var consumeEvent = ConsumeEventName(producer, options.Event);
        conn.On(consumeEvent, obj => Deliver(obj, handler, autoAck));
        chunking.OnAssembled(consumeEvent, obj => Deliver(obj, handler, autoAck));

        conn.Emit("SUBSCRIBE", payload);
    }

    /// <summary>Raw wire escape hatch, like the other clients' emit.</summary>
    public void Emit(string @event, JsonNode? payload) => conn.Emit(@event, payload);

    /// <summary>Raw wire escape hatch for server events.</summary>
    public void On(string @event, Action<JsonNode?> handler) => conn.On(@event, handler);

    public void Disconnect() => conn.Disconnect();

    public void Dispose() => conn.Dispose();

    // internal for unit tests: the exact SUBSCRIBE wire payload
    internal static JsonObject BuildSubscribePayload(SubscribeOptions o)
    {
        var producer = o.Producer;
        if (o.Mode == "topic" && string.IsNullOrEmpty(producer))
            producer = AllProducers;
        if (string.IsNullOrEmpty(producer) || string.IsNullOrEmpty(o.Event) || string.IsNullOrEmpty(o.Consumer))
            throw new ArgumentException("subscribe: Producer, Event, and Consumer are required");

        var payload = new JsonObject
        {
            ["event"] = o.Event,
            ["producer"] = producer,
            ["consumer"] = o.Consumer,
            ["scope"] = "default",
            // Durable queues are keyed by consumer_id server-side; an omitted
            // value falls back to the ephemeral socket id and loses replay.
            ["consumer_id"] = o.ConsumerId ?? o.Consumer,
        };
        if (o.Durable) payload["durable"] = true;
        if (o.Ack || o.ManualAck) payload["ack"] = true;
        if (o.ManualAck) payload["manual_ack"] = true;
        if (o.AckTimeout != null) payload["ack_timeout"] = o.AckTimeout;
        if (o.Retry != null)
        {
            payload["retry"] = new JsonObject
            {
                ["max_attempts"] = o.Retry.MaxAttempts,
                ["delay"] = o.Retry.Delay,
                ["backoff"] = o.Retry.Backoff,
            };
        }
        if (o.Mode != null) payload["mode"] = o.Mode;
        if (o.Group != null) payload["group"] = o.Group;
        return payload;
    }

    private void Deliver(JsonNode? obj, ConsumeHandler handler, bool autoAck)
    {
        if (obj is not JsonObject raw)
            return;
        var message = raw["message"];
        var from = raw["from"]?.GetValue<string>();
        var msgId = (raw["msgId"] ?? raw["msg_id"])?.GetValue<string>();

        var acked = false;
        void AckOnce()
        {
            if (msgId == null || acked)
                return;
            acked = true;
            Ack(msgId);
        }

        try
        {
            handler(message, from, AckOnce, raw);
        }
        catch (Exception e)
        {
            // No auto-ACK on a failed handler: the server retries on its
            // schedule and dead-letters when attempts are exhausted.
            Console.Error.WriteLine($"tyo-mq: consume handler failed: {e.Message}");
            return;
        }
        if (autoAck)
            AckOnce();
    }
}
