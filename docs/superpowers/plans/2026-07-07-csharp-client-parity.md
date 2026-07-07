# tyo-mq-client-csharp Parity Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite the C# client to full tyo-mq parity (per spec `docs/superpowers/specs/2026-07-07-csharp-client-parity-design.md`) so all 21 UNSUPPORTED conformance cells turn PASS while `tyostocks-datacollector` keeps compiling.

**Architecture:** BCL-only Engine.IO 4 / Socket.IO v4 transport (`ClientWebSocket`), `TyoMq.Client` protocol layer, chunking module, `TYO_MQ_CLIENT` compat wrappers over the new core. SocketIOClient dependency removed.

**Tech Stack:** .NET 8, System.Text.Json (JsonNode), xunit test project (new), conformance suite as acceptance harness.

**Wire ground truth (verified today — do not re-derive):**
- EIO4 ws URL: `{ws|wss}://host:port/socket.io/?EIO=4&transport=websocket`. Server → `0{"sid":...,"pingInterval":...}`; client → `40` (SIO connect); server → `40{...}`. Server pings `2` → client pongs `3`. Events: `42["EVENT",payload]` (payload may be absent). Reference: Go `client.go` (readHandshake/readSIOConnected/handleFrame/Emit).
- Registration: `PRODUCER {"name":n}` · `CONSUMER {"name":n,"id":n,"consumer_id":n}`.
- Auth: `AUTHENTICATION {"token":t}` → `AUTH_OK {realm,role}` / `AUTH_FAIL {code,message}`.
- Produce: `PRODUCE {"event":e,"message":m,"from":p}`. Ack: `ACK {"msgId":id}`.
- SUBSCRIBE payload keys: `event, producer, consumer, scope:"default", consumer_id` (+ `durable,ack,manual_ack,ack_timeout,retry{max_attempts,delay,backoff},mode,group` when set). `ack` must be true when `manual_ack` is. `consumer_id` defaults to consumer name. Topic mode: producer = `TYO-MQ-ALL`.
- Delivery event name: `CONSUME-` + lower(`producer-event`) (scope all: `CONSUME-` + lower(producer) + `-TM-ALL`). Delivery object: `{event, message, from, msgId}`. Auto-ACK = ack && !manual_ack, after non-throwing handler; throwing handler → no ACK.
- Chunking: outbound `PRODUCE_CHUNK {transferId,total,index,data,event,from}` for JSON > 256*1024 chars (mirror Node `lib/publisher.js:53-120` exactly); inbound `CONSUME_CHUNK {transferId,total,index,data,event}` reassembled then dispatched as if a normal delivery on `event`.
- Old-API surface that must survive in Compat/: `Publisher(name, eventDefault?, host?, port?, protocol?)`, `Subscriber(name, host?, port?, protocol?)`, `Task register(Delegate? cb=null, int waittime=-1)`, `void on(string, Delegate)`, `bool connected`, `void send_identification_info()`, `produce(string data, string? event=null, string? method=null)`, `subscribe(who, event?, Delegate?, bool reconnect=true)`, `disconnect()`.

---

### Task 1: xunit test project + SIO frame codec (TDD)
**Files:** `tyo-mq-client-tests/tyo-mq-client-tests.csproj` (xunit, ProjectReference to client), add to `.sln`; `tyo-mq-client-csharp/Transport/SioCodec.cs`.
- [ ] Failing tests: encode event frame (`42["PRODUCE",{...}]`), decode event frame with/without payload, decode handshake `0{json}`, `40{json}`, ping `2`.
- [ ] Implement static `SioCodec` (EncodeEvent, TryDecode → {Open,SioConnected,Ping,Event(name,JsonNode?)}).
- [ ] `dotnet test` green. Commit `feat: SIO v4 frame codec + test project`.

### Task 2: transport — EngineIo + SocketIo
**Files:** `Transport/SocketIoConnection.cs` (owns ClientWebSocket, receive loop Task, ping→pong, `ConnectAsync`, thread-safe `EmitAsync/Emit`, `On(event, Action<JsonNode?>)`, `Connected/Disconnected` events, `Dispose`).
- [ ] Implement (reference Go client.go flow). Buffer up to 4 MB frames (chunk frames are ≤ ~256 KB + envelope).
- [ ] Smoke-test against a live server started via the conformance harness (`node -e` one-liner or a small script): connect, emit PRODUCER, disconnect. Commit `feat: BCL Socket.IO v4 transport`.

### Task 3: `TyoMq.Client` protocol layer
**Files:** `Client.cs`, `SubscribeOptions.cs` (with `RetryPolicy`, `ConsumeHandler`), `TyoMqConstants.cs` (AllProducers, EventAll, DefaultPort 17352).
- [ ] Implement per spec API: ConnectAsync/AuthenticateAsync (WaitFor AUTH_OK/AUTH_FAIL, 10s default timeout)/RegisterProducerAsync/RegisterConsumerAsync (both just emit + brief settle, matching other clients)/Produce/Subscribe/Ack/Emit/On/Disconnect.
- [ ] Subscribe: build payload per wire truth; register consume-event handler; wrap handler with msgId extraction + auto-ACK/no-ACK-on-throw + manual-ack Action.
- [ ] Unit-test payload construction via an internal `BuildSubscribePayload` (visible to tests): defaults (`consumer_id`=consumer, ack forced by manual_ack, topic → producer TYO-MQ-ALL). Commit `feat: TyoMq.Client protocol layer`.

### Task 4: chunking
**Files:** `Chunking.cs` wired into Client.Produce (outbound) and SocketIoConnection dispatch (inbound CONSUME_CHUNK).
- [ ] Unit tests: split/reassemble round trip at 256 KB boundary; out-of-order chunks; two interleaved transfers.
- [ ] Implement mirroring Node publisher.js / socket.js. Commit `feat: PRODUCE_CHUNK/CONSUME_CHUNK parity`.

### Task 5: compat wrappers + remove old stack
**Files:** create `Compat/Publisher.cs`, `Compat/Subscriber.cs` (namespace `TYO_MQ_CLIENT`, surface per wire truth above, implemented over `TyoMq.Client`); delete `Socket.cs, Event.cs, Events.cs, Message.cs, Subscription.cs, Factory.cs, Publisher.cs, Subscriber.cs` (old); trim `Constants.cs/Utils.cs/Logger.cs`; csproj: drop SocketIOClient, version 2.0.0.
- [ ] `send_identification_info`: replicate current behavior (read what Socket.cs:163 sends before deleting; keep the same IDENTIFICATION emit).
- [ ] Build client; `dotnet build` the two sample projects and `/data/tyolab/csharp/tyostocks-datacollector` against a local ProjectReference override (`dotnet build -p:...` or temp sln) — all compile.
- [ ] Commit `feat!: v2 core + TYO_MQ_CLIENT compat wrappers (drop SocketIOClient)`.

### Task 6: conformance runner rewrite + acceptance
**Files:** `tyo-mq-conformance/runners/csharp/Program.cs` (rewrite on TyoMq.Client: full command set incl. durable/manual-ack/topic/group/token; delete `caps` + unsupported replies).
- [ ] `node harness/run.js --langs node,csharp` → all PASS.
- [ ] Full matrix `node harness/run.js` → **111 PASS, 0 FAIL, 0 UNSUPPORTED**; commit REPORT.md in conformance repo.
- [ ] Update client README (features table like Go's; drop the SocketIOClient pin note; keep conformance link); update tyo-mq server README C# row if wording changes. Commit both repos.

**Self-review:** spec §API↔Task 3, §chunking↔Task 4, §compat↔Task 5, §verification↔Task 6 all covered; no placeholders; names consistent (`TyoMq.Client`, `SubscribeOptions`, `SioCodec`, `SocketIoConnection`).
