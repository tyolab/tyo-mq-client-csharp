using System.Text.Json.Nodes;

namespace TyoMq;

/// <summary>Receives one delivered message.</summary>
/// <param name="message">The produced payload (object, array, string, number, …).</param>
/// <param name="from">The producer's name (null when the server omits it).</param>
/// <param name="ack">Acknowledges this delivery. A no-op when the delivery
/// carries no msgId. With auto-ACK (Ack without ManualAck) it is invoked for
/// you after the handler returns without throwing.</param>
/// <param name="raw">The full delivery object (event, msgId, …).</param>
public delegate void ConsumeHandler(JsonNode? message, string? from, Action ack, JsonObject raw);

/// <summary>Retry schedule for ACK-enabled durable subscriptions.</summary>
public sealed class RetryPolicy
{
    public int MaxAttempts { get; init; }
    /// <summary>Duration string, e.g. "5s" or "200ms".</summary>
    public string Delay { get; init; } = "5s";
    /// <summary>"" or "exponential".</summary>
    public string Backoff { get; init; } = "";
}

/// <summary>
/// A subscription request. The defaults give plain fire-and-forget delivery;
/// the optional members opt in to guaranteed delivery and routing features,
/// matching the other tyo-mq clients.
/// </summary>
public sealed class SubscribeOptions
{
    /// <summary>Producer name. Null with Mode = "topic" means any producer.</summary>
    public string? Producer { get; init; }

    /// <summary>Event name — or an MQTT-style pattern when Mode is "topic".</summary>
    public required string Event { get; init; }

    public required string Consumer { get; init; }

    /// <summary>Durable consumer identity; defaults to Consumer. Reconnect
    /// with the same identity to replay queued durable messages.</summary>
    public string? ConsumerId { get; init; }

    public bool Durable { get; init; }

    /// <summary>Auto-ACK each delivery after the handler returns.</summary>
    public bool Ack { get; init; }

    /// <summary>The handler must ack itself (its ack argument or Client.Ack).</summary>
    public bool ManualAck { get; init; }

    /// <summary>e.g. "30s".</summary>
    public string? AckTimeout { get; init; }

    public RetryPolicy? Retry { get; init; }

    /// <summary>"topic" treats Event as an MQTT-style pattern (+ one level, # the rest).</summary>
    public string? Mode { get; init; }

    /// <summary>Consumer group name; group members load-balance deliveries.</summary>
    public string? Group { get; init; }
}
