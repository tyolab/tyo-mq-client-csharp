using System.Text.Json.Nodes;

namespace TYO_MQ_CLIENT;

/// <summary>
/// v1 API preserved for existing consumers (e.g. tyostocks-datacollector),
/// now a thin wrapper over TyoMq.Client. Payloads keep v1 semantics: the
/// string you pass to produce() is delivered as a string message.
/// </summary>
public class Publisher : CompatSocket
{
    private readonly string? eventDefault;

    public Publisher(string name, string? eventDefault = null, string? host = null,
                     int port = -1, string? protocol = null)
        : base("PRODUCER", name, host, port, protocol)
    {
        this.eventDefault = eventDefault;
    }

    public string get_default_event() =>
        !string.IsNullOrEmpty(eventDefault) ? eventDefault! : $"{name}-{Constants.EVENT_DEFAULT}";

    public void produce(string data, string? eventName = null, string? method = null)
    {
        if (data == null)
            return;
        eventName ??= eventDefault
            ?? throw new Exception("please specify a topic of the message or specify one when creating a publisher");
        client.Produce(name, eventName, JsonValue.Create(data));
    }
}
