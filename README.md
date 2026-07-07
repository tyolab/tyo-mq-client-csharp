# tyo-mq-client-csharp

A .NET client for **[tyo-mq](https://github.com/tyolab/tyo-mq)** — the
distributed pub/sub messaging service with durable delivery, MQTT-style topic
routing, consumer groups, and multi-tenant auth realms.

Targets .NET 8, built on [SocketIOClient](https://www.nuget.org/packages/SocketIOClient)
(Socket.IO v4). Large messages are chunked automatically in both directions
(256 KB frames), matching the Node.js client.

## Install

```bash
dotnet add package TYO_MQ_CLIENT
```

You'll need a running tyo-mq server:
`npm install tyo-mq && node -e "new (require('tyo-mq').Server)().start()"`,
or Docker — see the [server repo](https://github.com/tyolab/tyo-mq).

> **Note:** this client pins **SocketIOClient 3.0.6** — currently the only
> version verified against the tyo-mq server. Later majors changed the EIO
> option surface; upgrade deliberately and re-test.

## Quick start

**Produce:**

```csharp
using TYO_MQ_CLIENT;

var publisher = new Publisher("order-service", "order-placed" /* default event */);
await publisher.register(() => Console.WriteLine("connected"));

publisher.produce("{\"orderId\": 1001}");               // default event
publisher.produce("{\"orderId\": 1002}", "order-paid"); // named event
```

**Subscribe:**

```csharp
using TYO_MQ_CLIENT;

var subscriber = new Subscriber("email-service");
await subscriber.register(() => Console.WriteLine("connected"));

subscriber.subscribe("order-service", "order-placed", (object msg) => {
    Console.WriteLine($"received: {msg}");
});

// or every event from that producer:
subscriber.subscribe("order-service", (object msg) => { /* ... */ });
```

Both `Publisher` and `Subscriber` accept `host` / `port` / `protocol`
constructor arguments; the default is `localhost:17352`.

## Runnable samples

The solution includes a sample producer and subscriber:

```bash
dotnet run --project tyo-mq-client-sample-subscriber/tyo-mq-client-sample-subscriber.csproj
dotnet run --project tyo-mq-client-sample-producer/tyo-mq-client-sample-producer.csproj
```

Point them at a non-default server with environment variables:

```bash
TYO_MQ_HOST=10.0.0.5 TYO_MQ_PORT=17390 dotnet run --project ...
```

The producer publishes a timestamped message every second; the subscriber
prints everything it receives.

## Server features

Durable delivery with ACK/retry/dead-lettering, topic wildcards, consumer
groups, broadcast, and authentication realms are provided by the server and
addressed through the wire protocol — see the
[server documentation](https://github.com/tyolab/tyo-mq) for the message
formats. This client currently covers the core produce/subscribe flow;
the richer options are being brought over (contributions welcome).

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

MIT (see LICENSE). Built by [TYO Lab](https://tyo.com.au).
