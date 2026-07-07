# tyo-mq-client-csharp

A .NET client for **[tyo-mq](https://github.com/tyolab/tyo-mq)** — the
distributed pub/sub messaging service with durable delivery (ACK / retry /
dead-letter queue), MQTT-style topic wildcards, consumer groups, and
multi-tenant auth realms.

Targets .NET 8. **No external dependencies** — the client ships its own
Socket.IO v4 transport over `System.Net.WebSockets`. Large messages are
chunked automatically in both directions (256 KB frames), matching the
Node.js client.

## Install

```bash
dotnet add package TYO_MQ_CLIENT
```

You'll need a running tyo-mq server:
`npm install tyo-mq && node -e "new (require('tyo-mq').Server)().start()"`,
or Docker — see the [server repo](https://github.com/tyolab/tyo-mq).

## Quick start

```csharp
using System.Text.Json.Nodes;
using TyoMq;

var producer = new Client("http://localhost:17352");
await producer.ConnectAsync();
// with auth enabled on the server:
// await producer.AuthenticateAsync("my-token");
await producer.RegisterProducerAsync("order-service");
producer.Produce("order-service", "order-placed",
    new JsonObject { ["orderId"] = 1001, ["total"] = 129.0 });

var consumer = new Client("http://localhost:17352");
await consumer.ConnectAsync();
await consumer.RegisterConsumerAsync("email-service");
consumer.Subscribe(new SubscribeOptions
{
    Producer = "order-service",
    Event = "order-placed",
    Consumer = "email-service",
    Durable = true,
    Ack = true,   // auto-ACKed after the handler returns
    Retry = new RetryPolicy { MaxAttempts = 3, Delay = "5s", Backoff = "exponential" },
}, (message, from, ack, raw) =>
{
    Console.WriteLine($"order event from {from}: {message} (msgId: {raw["msgId"]})");
});
```

With `ManualAck = true` (plus e.g. `AckTimeout = "30s"`) the handler must
call its `ack` argument (or `client.Ack(msgId)`) itself; unacked deliveries
retry per the policy and dead-letter when attempts are exhausted.

## Topics, groups

```csharp
// MQTT-style wildcards: + is one level, # is the rest
consumer.Subscribe(new SubscribeOptions
{
    Event = "orders/+/status", Consumer = "dashboard", Mode = "topic",
}, handler);

// consumer groups load-balance across workers
consumer.Subscribe(new SubscribeOptions
{
    Producer = "dispatcher", Event = "jobs", Consumer = "worker-1", Group = "workers",
}, handler);
```

Anything not covered by a helper is one `client.Emit(event, payload)` +
`client.On(event, handler)` away — the full wire protocol is documented in
the [server repo](https://github.com/tyolab/tyo-mq).

## v1 compatibility

The 1.x `TYO_MQ_CLIENT.Publisher` / `Subscriber` API still works — the
classes are thin wrappers over `TyoMq.Client`, and v1 string-payload
semantics are preserved. New code should use `TyoMq.Client`.

## Runnable samples

```bash
dotnet run --project tyo-mq-client-sample-subscriber/tyo-mq-client-sample-subscriber.csproj
dotnet run --project tyo-mq-client-sample-producer/tyo-mq-client-sample-producer.csproj
```

Point them at a non-default server with `TYO_MQ_HOST` / `TYO_MQ_PORT`.

## Other clients

Node.js (and browsers) ships with the [server package](https://github.com/tyolab/tyo-mq);
see also [Python](https://github.com/tyolab/tyo-mq-client-python),
[Rust](https://github.com/tyolab/tyo-mq-client-rust),
[C/C++](https://github.com/tyolab/tyo-mq-client-cpp),
[Ruby](https://github.com/tyolab/tyo-mq-client-ruby),
[Go](https://github.com/tyolab/tyo-mq-client-go), and
[Java](https://github.com/tyolab/tyo-mq-client-java).

All clients are exercised together by the cross-language
[conformance suite](https://github.com/tyolab/tyo-mq-conformance), which runs
the same pub/sub, durable-delivery, topic, group, and auth scenarios against
every client (and every producer/consumer language pair) and publishes the
resulting matrix.

## License

Apache-2.0. Built by [TYO Lab](https://tyo.com.au).
