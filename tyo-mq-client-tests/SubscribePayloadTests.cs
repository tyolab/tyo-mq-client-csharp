using System.Text.Json.Nodes;
using TyoMq;
using Xunit;

public class SubscribePayloadTests
{
    [Fact]
    public void ConsumerIdDefaultsToConsumerName()
    {
        var p = Client.BuildSubscribePayload(new SubscribeOptions
        {
            Producer = "orders", Event = "placed", Consumer = "email-svc",
        });
        Assert.Equal("email-svc", (string)p["consumer_id"]!);
        Assert.Equal("default", (string)p["scope"]!);
        Assert.Null(p["durable"]);
        Assert.Null(p["ack"]);
    }

    [Fact]
    public void ManualAckForcesAck()
    {
        var p = Client.BuildSubscribePayload(new SubscribeOptions
        {
            Producer = "orders", Event = "placed", Consumer = "c",
            ManualAck = true, AckTimeout = "30s",
            Retry = new RetryPolicy { MaxAttempts = 3, Delay = "5s", Backoff = "exponential" },
        });
        Assert.True((bool)p["ack"]!);
        Assert.True((bool)p["manual_ack"]!);
        Assert.Equal("30s", (string)p["ack_timeout"]!);
        Assert.Equal(3, (int)p["retry"]!["max_attempts"]!);
        Assert.Equal("exponential", (string)p["retry"]!["backoff"]!);
    }

    [Fact]
    public void TopicModeDefaultsProducerToAllProducers()
    {
        var p = Client.BuildSubscribePayload(new SubscribeOptions
        {
            Event = "orders/+/status", Consumer = "dash", Mode = "topic",
        });
        Assert.Equal(Client.AllProducers, (string)p["producer"]!);
        Assert.Equal("topic", (string)p["mode"]!);
    }

    [Fact]
    public void GroupAndDurableAreForwarded()
    {
        var p = Client.BuildSubscribePayload(new SubscribeOptions
        {
            Producer = "d", Event = "jobs", Consumer = "w1",
            Durable = true, Ack = true, Group = "workers",
        });
        Assert.True((bool)p["durable"]!);
        Assert.True((bool)p["ack"]!);
        Assert.Equal("workers", (string)p["group"]!);
        Assert.Null(p["manual_ack"]);
    }

    [Fact]
    public void ConsumeEventNameMatchesProtocol()
    {
        Assert.Equal("CONSUME-orders-placed", Client.ConsumeEventName("Orders", "Placed"));
        Assert.Equal("CONSUME-orders-TM-ALL", Client.ConsumeEventName("Orders", "x", "all"));
    }
}
