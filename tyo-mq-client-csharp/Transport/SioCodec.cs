using System.Text.Json;
using System.Text.Json.Nodes;

namespace TyoMq.Transport;

public enum SioMessageKind
{
    Open,          // Engine.IO "0{handshake}"
    Ping,          // Engine.IO "2"
    SioConnected,  // Socket.IO "40{...}"
    Event,         // Socket.IO "42[\"name\",payload?]"
    Other,
}

public readonly struct SioMessage
{
    public SioMessageKind Kind { get; init; }
    public string? EventName { get; init; }
    public JsonNode? Payload { get; init; }
}

/// <summary>
/// Encodes and decodes the Engine.IO 4 / Socket.IO v4 text frames tyo-mq
/// uses (default namespace, websocket transport, no acks/binary).
/// </summary>
public static class SioCodec
{
    public static string EncodeEvent(string eventName, JsonNode? payload)
    {
        var arr = new JsonArray { JsonValue.Create(eventName) };
        if (payload != null)
            arr.Add(payload.DeepClone());
        return "42" + arr.ToJsonString();
    }

    public const string EnginePong = "3";
    public const string SioConnect = "40";

    public static SioMessage Decode(string frame)
    {
        if (frame.Length == 0)
            return new SioMessage { Kind = SioMessageKind.Other };

        if (frame[0] == '0')
            return new SioMessage { Kind = SioMessageKind.Open, Payload = ParseOrNull(frame[1..]) };

        if (frame == "2")
            return new SioMessage { Kind = SioMessageKind.Ping };

        if (frame.StartsWith("40"))
            return new SioMessage { Kind = SioMessageKind.SioConnected, Payload = ParseOrNull(frame[2..]) };

        if (frame.StartsWith("42"))
        {
            if (ParseOrNull(frame[2..]) is JsonArray arr && arr.Count >= 1 && arr[0] is JsonValue name)
            {
                return new SioMessage
                {
                    Kind = SioMessageKind.Event,
                    EventName = name.GetValue<string>(),
                    Payload = arr.Count > 1 ? arr[1]?.DeepClone() : null,
                };
            }
        }

        return new SioMessage { Kind = SioMessageKind.Other };
    }

    private static JsonNode? ParseOrNull(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
