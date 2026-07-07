using System.Text.Json.Nodes;
using TyoMq;
using Xunit;

public class ChunkingTests
{
    private static (Chunking, List<(string Ev, JsonNode? Payload)>) Capture()
    {
        var sent = new List<(string, JsonNode?)>();
        var chunking = new Chunking((ev, payload) => sent.Add((ev, payload)));
        return (chunking, sent);
    }

    [Fact]
    public void SmallProduceIsOneFrame()
    {
        var (chunking, sent) = Capture();
        chunking.Produce("p", "e", JsonNode.Parse("""{"a":1}"""));
        var (ev, payload) = Assert.Single(sent);
        Assert.Equal("PRODUCE", ev);
        Assert.Equal("p", (string)payload!["from"]!);
        Assert.Equal(1, (int)payload!["message"]!["a"]!);
    }

    [Fact]
    public void LargeProduceSplitsAndSlicesReassembleToEnvelope()
    {
        var (chunking, sent) = Capture();
        var big = new string('x', Chunking.ChunkSize + 1000);
        chunking.Produce("p", "e", JsonValue.Create(big));

        Assert.True(sent.Count >= 2);
        Assert.All(sent, f => Assert.Equal("PRODUCE_CHUNK", f.Ev));
        var joined = string.Concat(sent.Select(f => (string)f.Payload!["data"]!));
        var envelope = JsonNode.Parse(joined)!;
        Assert.Equal("e", (string)envelope["event"]!);
        Assert.Equal(big, (string)envelope["message"]!);
        Assert.Equal(sent.Count, (int)sent[0].Payload!["total"]!);
        Assert.Equal(0, (int)sent[0].Payload!["index"]!);
    }

    [Fact]
    public void ConsumeChunksReassembleOutOfOrderAndInterleaved()
    {
        var (chunking, _) = Capture();
        var got = new List<string>();
        chunking.OnAssembled("CONSUME-p-e", obj => got.Add((string)obj!["message"]!));

        JsonObject Chunk(string id, int index, int total, string data) => new()
        {
            ["transferId"] = id, ["index"] = index, ["total"] = total,
            ["data"] = data, ["event"] = "CONSUME-p-e",
        };

        // two interleaved transfers, chunks out of order
        var a = """{"message":"AAAA","from":"p"}""";
        var b = """{"message":"BBBB","from":"p"}""";
        chunking.OnConsumeChunk(Chunk("t-a", 1, 2, a[14..]));
        chunking.OnConsumeChunk(Chunk("t-b", 0, 2, b[..14]));
        chunking.OnConsumeChunk(Chunk("t-a", 0, 2, a[..14]));
        Assert.Equal(new[] { "AAAA" }, got);
        chunking.OnConsumeChunk(Chunk("t-b", 1, 2, b[14..]));
        Assert.Equal(new[] { "AAAA", "BBBB" }, got);
    }

    [Fact]
    public void DuplicateChunksAreIgnored()
    {
        var (chunking, _) = Capture();
        var got = 0;
        chunking.OnAssembled("CONSUME-p-e", _ => got++);
        var payload = """{"message":1}""";
        var c0 = new JsonObject { ["transferId"] = "t", ["index"] = 0, ["total"] = 2, ["data"] = payload[..6], ["event"] = "CONSUME-p-e" };
        chunking.OnConsumeChunk(c0);
        chunking.OnConsumeChunk((JsonObject)c0.DeepClone());
        Assert.Equal(0, got);
        chunking.OnConsumeChunk(new JsonObject { ["transferId"] = "t", ["index"] = 1, ["total"] = 2, ["data"] = payload[6..], ["event"] = "CONSUME-p-e" });
        Assert.Equal(1, got);
    }
}
