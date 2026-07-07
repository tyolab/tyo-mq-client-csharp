# tyo-mq-client-csharp Full Parity Rewrite — Design

**Date:** 2026-07-07
**Status:** Decisions made autonomously while the user was away (flagged *[decision]*);
the user chose this project and the compat question timed out — the recommended
option was taken.

## Purpose

The C# client is a generation behind the other seven tyo-mq clients: string-only
payloads, no durable delivery/ACK/retry/DLQ, no topic wildcards, no consumer
groups, no auth. The 2026-07-07 conformance matrix shows 21 UNSUPPORTED cells for
C#. This rewrite brings it to full parity. **Acceptance: every C# cell in
`tyo-mq-conformance` turns PASS** (S1 full fixtures, S2, S3, S4, S5, S6).

## Constraints

- **Production consumer:** `tyostocks-datacollector` uses the published
  `TYO_MQ_CLIENT` package: `new Publisher/Subscriber(name, host, port)`,
  `register()`, `on("connect", ...)`, `send_identification_info()`, `.connected`,
  `produce(string, string)`, `subscribe(who, event, handler)`. This surface must
  keep compiling and working unchanged.
- **Chunking wire compat:** `PRODUCE_CHUNK` (outbound > 256 KB) and
  `CONSUME_CHUNK` (inbound reassembly, keyed by `transferId`, fields
  `total/index/data/event`) must stay compatible with the Node server/client.
- Wire semantics reference: server `tests/phase1..6` + the Go/Java/Rust clients
  (SUBSCRIBE payload keys: `event, producer, consumer, scope, consumer_id,
  durable, ack, manual_ack, ack_timeout, retry{max_attempts,delay,backoff},
  mode, group`). **`consumer_id` must default to the consumer name** — the
  omission of this was a replay-losing bug found in the Go client today.

## Approach (*[decision]* — A of three)

New dependency-light core + new idiomatic API + thin compat wrappers, one
package (`TYO_MQ_CLIENT`, version 2.0.0), **SocketIOClient dependency removed**.
The transport is a hand-rolled Engine.IO 4 / Socket.IO v4 websocket client over
BCL `System.Net.WebSockets.ClientWebSocket` — the same pattern as the Go
(gorilla/websocket), Rust, C++, and Ruby clients, all written recently and
usable as references. Rejected: evolving Publisher/Subscriber in place (keeps
string-only ergonomics and the SocketIOClient 3.0.6 pin); clean break (orphans
datacollector).

## New API (namespace `TyoMq`)

```csharp
public delegate void ConsumeHandler(JsonNode? message, string? from, Action ack, JsonObject raw);

public sealed class Client : IDisposable
{
    public Client(string url);                    // "http://host:port"
    public Client(string host, int port, string protocol = "http");

    public Task ConnectAsync(CancellationToken ct = default);   // EIO+SIO handshake complete
    public Task AuthenticateAsync(string token, CancellationToken ct = default); // AUTH_OK/AUTH_FAIL
    public Task RegisterProducerAsync(string name, CancellationToken ct = default);
    public Task RegisterConsumerAsync(string name, CancellationToken ct = default);

    public void Produce(string producer, string @event, JsonNode? payload); // chunks > 256 KB
    public void Subscribe(SubscribeOptions options, ConsumeHandler handler);
    public void Ack(string msgId);
    public void Emit(string @event, JsonNode payload);           // escape hatch
    public void On(string @event, Action<JsonNode?> handler);    // escape hatch
    public event Action? Connected;
    public event Action? Disconnected;
    public bool IsConnected { get; }
    public void Disconnect();
}

public sealed class SubscribeOptions
{
    public string? Producer;      // null + Mode=="topic" → ALL producers
    public required string Event; // event name or topic pattern
    public required string Consumer;
    public string? ConsumerId;    // defaults to Consumer
    public bool Durable, Ack, ManualAck;
    public string? AckTimeout;    // "30s"
    public RetryPolicy? Retry;    // MaxAttempts, Delay, Backoff
    public string? Mode;          // "topic"
    public string? Group;
}
```

Semantics identical to the other clients: `Ack && !ManualAck` auto-ACKs after
the handler returns without throwing; a throwing handler is not ACKed (server
retries/dead-letters); `ManualAck` hands the `ack` closure to the handler and
`Client.Ack(msgId)` also works. Registration/auth methods fail their `Task` on
`AUTH_FAIL`/server error or a 10s timeout.

## Internal layout (all in the existing csproj)

| File | Responsibility |
|---|---|
| `Transport/EngineIo.cs` | ws connect (`/socket.io/?EIO=4&transport=websocket`), open packet parse, ping→pong, close |
| `Transport/SocketIo.cs` | SIO v4 frames over EIO (`40` connect, `42["event",data]` emit/dispatch), thread-safe send, receive loop task |
| `Client.cs` | tyo-mq protocol: register/auth/produce/subscribe/ack, consume-event routing (`CONSUME-<producer>-<event>` lowercase, topic + ALL variants per Events rules in the existing clients) |
| `SubscribeOptions.cs` | options + `RetryPolicy` + `ConsumeHandler` |
| `Chunking.cs` | `PRODUCE_CHUNK` split / `CONSUME_CHUNK` reassembly |
| `Compat/Publisher.cs`, `Compat/Subscriber.cs` | old `TYO_MQ_CLIENT` API preserved verbatim over `TyoMq.Client` (incl. `register()`, `on`, `connected`, `send_identification_info`, string `produce`, basic `subscribe`) |

Old `Socket.cs`/`Event*.cs`/`Message.cs`/`Subscription.cs` are deleted; anything
the wrappers still need moves into `Compat/`. `Logger.cs`, `Constants.cs`,
`Utils.cs` are kept/trimmed as needed.

## Error handling

- Receive-loop failures fire `Disconnected` and fail in-flight waits; no
  auto-reconnect in the core (matches Go/Rust: reconnection is the caller's
  policy). The compat `Subscriber.register()` keeps whatever retry behavior it
  has today.
- Server `ERROR`/`AUTH_FAIL` payloads surface as exceptions on the awaiting
  call; async delivery errors log to `Logger`.

## Verification

1. Conformance runner (`runners/csharp`) rewritten on `TyoMq.Client`; `caps`
   command and all `unsupported:` replies removed. Full matrix run: **111 PASS,
   0 FAIL, 0 UNSUPPORTED** (90 existing + 21 flipped).
2. `tyostocks-datacollector` compiles against the rewritten project
   (ProjectReference build check).
3. The repo's sample producer/subscriber projects still build and run.
4. Package metadata: version 2.0.0, SocketIOClient reference removed.

## Non-goals

- No auto-reconnect/backoff layer in the new core (callers own it).
- No remote-namespace (`/remote`) helper, signed admin commands, or broadcast
  helpers beyond `Emit` in v2.0 — same "one emit away" posture as the Go client
  README documents. (Broadcast can ride `Emit`.)
- No NuGet publish in this task (publish.sh exists; publishing is the user's call).
