using System.Text.Json.Nodes;
using TyoMq.Transport;
using Xunit;

public class SioCodecTests
{
    [Fact]
    public void EncodesEventWithPayload()
    {
        var frame = SioCodec.EncodeEvent("PRODUCE", JsonNode.Parse("""{"event":"e1","message":42,"from":"p"}"""));
        Assert.Equal("""42["PRODUCE",{"event":"e1","message":42,"from":"p"}]""", frame);
    }

    [Fact]
    public void EncodesEventWithoutPayload()
    {
        Assert.Equal("""42["DISCONNECT"]""", SioCodec.EncodeEvent("DISCONNECT", null));
    }

    [Fact]
    public void DecodesEventWithPayload()
    {
        var msg = SioCodec.Decode("""42["CONSUME-p-e1",{"message":{"a":1},"from":"p","msgId":"m1"}]""");
        Assert.Equal(SioMessageKind.Event, msg.Kind);
        Assert.Equal("CONSUME-p-e1", msg.EventName);
        Assert.Equal(1, (int)msg.Payload!["message"]!["a"]!);
        Assert.Equal("m1", (string)msg.Payload!["msgId"]!);
    }

    [Fact]
    public void DecodesEventWithoutPayload()
    {
        var msg = SioCodec.Decode("""42["PING-ME"]""");
        Assert.Equal(SioMessageKind.Event, msg.Kind);
        Assert.Equal("PING-ME", msg.EventName);
        Assert.Null(msg.Payload);
    }

    [Fact]
    public void DecodesEngineOpen()
    {
        var msg = SioCodec.Decode("""0{"sid":"abc","pingInterval":5000,"pingTimeout":10000}""");
        Assert.Equal(SioMessageKind.Open, msg.Kind);
        Assert.Equal("abc", (string)msg.Payload!["sid"]!);
    }

    [Fact]
    public void DecodesSioConnected()
    {
        var msg = SioCodec.Decode("""40{"sid":"xyz"}""");
        Assert.Equal(SioMessageKind.SioConnected, msg.Kind);
    }

    [Fact]
    public void DecodesPing()
    {
        Assert.Equal(SioMessageKind.Ping, SioCodec.Decode("2").Kind);
    }

    [Fact]
    public void UnknownFramesAreOther()
    {
        Assert.Equal(SioMessageKind.Other, SioCodec.Decode("6").Kind);
        Assert.Equal(SioMessageKind.Other, SioCodec.Decode("").Kind);
    }
}
