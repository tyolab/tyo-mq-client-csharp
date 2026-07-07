using System.Text;
using System.Text.Json.Nodes;
using TyoMq.Transport;

namespace TyoMq;

/// <summary>
/// Large-message parity with the Node client: an outbound PRODUCE whose JSON
/// exceeds the chunk threshold is split into PRODUCE_CHUNK frames
/// (transferId/index/total/data slices of the whole serialized envelope);
/// inbound CONSUME_CHUNK frames are reassembled and dispatched to the
/// subscription handler of their target event.
/// </summary>
internal sealed class Chunking
{
    // Must match Publisher.CHUNK_SIZE in the JS server/client (256 KB).
    internal const int ChunkSize = 256 * 1024;

    private readonly Action<string, JsonNode?> send;
    private readonly object gate = new();
    private readonly Dictionary<string, Transfer> inbound = new();
    private readonly Dictionary<string, List<Action<JsonNode?>>> assembledHandlers = new();

    private sealed class Transfer
    {
        public required string[] Parts;
        public int Received;
        public required string Event;
    }

    public Chunking(SocketIoConnection conn)
        : this(conn.Emit)
    {
        conn.On("CONSUME_CHUNK", OnConsumeChunk);
    }

    // internal for unit tests: capture outbound frames without a socket
    internal Chunking(Action<string, JsonNode?> send)
    {
        this.send = send;
    }

    /// <summary>Sends a PRODUCE, chunked when the envelope is large.</summary>
    public void Produce(string producer, string @event, JsonNode? payload)
    {
        var envelope = new JsonObject
        {
            ["event"] = @event,
            ["message"] = payload?.DeepClone(),
            ["from"] = producer,
        };
        var str = envelope.ToJsonString();
        if (str.Length <= ChunkSize)
        {
            send("PRODUCE", envelope);
            return;
        }

        var total = (int)Math.Ceiling(str.Length / (double)ChunkSize);
        var transferId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x") +
                         "-" + Guid.NewGuid().ToString("N")[..10];
        for (var i = 0; i < total; i++)
        {
            var slice = str.Substring(i * ChunkSize, Math.Min(ChunkSize, str.Length - i * ChunkSize));
            send("PRODUCE_CHUNK", new JsonObject
            {
                ["transferId"] = transferId,
                ["index"] = i,
                ["total"] = total,
                ["data"] = slice,
            });
        }
    }

    /// <summary>Registers a handler for reassembled deliveries of an event.</summary>
    public void OnAssembled(string consumeEvent, Action<JsonNode?> handler)
    {
        lock (gate)
        {
            if (!assembledHandlers.TryGetValue(consumeEvent, out var list))
                assembledHandlers[consumeEvent] = list = new List<Action<JsonNode?>>();
            list.Add(handler);
        }
    }

    // internal for unit tests
    internal void OnConsumeChunk(JsonNode? node)
    {
        if (node is not JsonObject chunk)
            return;
        var transferId = chunk["transferId"]?.GetValue<string>();
        var total = chunk["total"]?.GetValue<int>() ?? 0;
        var index = chunk["index"]?.GetValue<int>() ?? -1;
        var data = chunk["data"]?.GetValue<string>();
        var @event = chunk["event"]?.GetValue<string>();
        if (transferId == null || data == null || @event == null || total <= 0 || index < 0 || index >= total)
            return;

        string? assembled = null;
        Action<JsonNode?>[] handlers = Array.Empty<Action<JsonNode?>>();
        lock (gate)
        {
            if (!inbound.TryGetValue(transferId, out var transfer))
                inbound[transferId] = transfer = new Transfer { Parts = new string[total], Event = @event };
            if (transfer.Parts[index] == null)
            {
                transfer.Parts[index] = data;
                transfer.Received++;
            }
            if (transfer.Received == total)
            {
                inbound.Remove(transferId);
                var sb = new StringBuilder();
                foreach (var part in transfer.Parts)
                    sb.Append(part);
                assembled = sb.ToString();
                if (assembledHandlers.TryGetValue(transfer.Event, out var list))
                    handlers = list.ToArray();
            }
        }

        if (assembled == null)
            return;
        JsonNode? obj;
        try
        {
            obj = JsonNode.Parse(assembled);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"tyo-mq: CONSUME_CHUNK reassembly parse failed: {e.Message}");
            return;
        }
        foreach (var h in handlers)
            h(obj);
    }
}
