using System.Text.Json.Nodes;

namespace TYO_MQ_CLIENT;

/// <summary>
/// v1 API preserved for existing consumers, wrapping TyoMq.Client. Handlers
/// receive the message payload; string messages arrive as strings (v1 wire
/// behavior), structured messages as their JSON text.
/// </summary>
public class Subscriber : CompatSocket
{
    public Subscriber(string name, string? host = null, int port = -1, string? protocol = null)
        : base("CONSUMER", name, host, port, protocol)
    {
    }

    /// <summary>Subscribes to one event from a producer (v1 3-arg form).</summary>
    public void subscribe(string who, string? eventName = null, Delegate? onConsumeCallback = null,
                          bool reconnect = true)
    {
        if (eventName == null || onConsumeCallback == null)
        {
            subscribeAll(who, onConsumeCallback ?? throw new ArgumentNullException(nameof(onConsumeCallback)));
            return;
        }
        client.Subscribe(new TyoMq.SubscribeOptions
        {
            Producer = who,
            Event = eventName,
            Consumer = name,
        }, (message, _, _, _) => InvokeCompat(onConsumeCallback, message));
    }

    /// <summary>Subscribes to every event from a producer (v1 2-arg form).</summary>
    public void subscribe(string who, Delegate? onConsumeCallback = null, bool reconnect = true) =>
        subscribeAll(who, onConsumeCallback ?? throw new ArgumentNullException(nameof(onConsumeCallback)));

    public void subscribeOnce(string who, string? eventName = null, Delegate? onConsumeCallback = null) =>
        subscribe(who, eventName, onConsumeCallback, false);

    public void subscribeAll(string who, Delegate onConsumeCallback)
    {
        client.On(TyoMq.Client.ConsumeEventName(who, "", "all"), obj =>
        {
            var message = obj is JsonObject o ? o["message"] : obj;
            InvokeCompat(onConsumeCallback, message);
        });
        client.Emit("SUBSCRIBE", new JsonObject
        {
            ["event"] = Constants.EVENT_ALL,
            ["producer"] = who,
            ["consumer"] = name,
            ["scope"] = Constants.SCOPE_ALL,
            ["consumer_id"] = name,
        });
    }

    private static void InvokeCompat(Delegate callback, JsonNode? message)
    {
        // v1 handlers took the message as their single argument, as a string.
        object? arg = message switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            _ => message.ToJsonString(),
        };
        var parameters = callback.Method.GetParameters();
        if (parameters.Length == 0)
            callback.DynamicInvoke();
        else
            callback.DynamicInvoke(new[] { arg });
    }
}
