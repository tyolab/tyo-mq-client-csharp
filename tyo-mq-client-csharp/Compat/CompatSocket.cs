using System.Text.Json.Nodes;

namespace TYO_MQ_CLIENT;

/// <summary>
/// Shared v1 connection surface (register / on / connected /
/// send_identification_info / disconnect) over the v2 TyoMq.Client core.
/// </summary>
public abstract class CompatSocket
{
    protected readonly TyoMq.Client client;
    protected readonly string name;
    private readonly string type;                 // "PRODUCER" | "CONSUMER"
    private readonly List<Delegate> connectListeners = new();
    private readonly object gate = new();

    public string uuid { get; } = Guid.NewGuid().ToString();
    public string socket_id => uuid;
    public bool debug { get; set; }
    public bool connected => client.IsConnected;

    protected CompatSocket(string type, string name, string? host, int port, string? protocol)
    {
        this.type = type;
        this.name = name;
        client = new TyoMq.Client(
            host ?? "localhost",
            port > 0 ? port : TyoMq.Client.DefaultPort,
            protocol ?? "http");
        client.Connected += OnConnected;
    }

    /// <summary>v2 core, for callers ready to move past the v1 surface.</summary>
    public TyoMq.Client Client => client;

    /// <summary>Connects and registers this identity (v1 name kept).</summary>
    public async Task register(Delegate? callback = null, int waittime = -1)
    {
        if (!client.IsConnected)
        {
            using var cts = waittime > 0 ? new CancellationTokenSource(waittime) : new CancellationTokenSource(10000);
            await client.ConnectAsync(cts.Token);
        }
        callback?.DynamicInvoke();
    }

    public void send_identification_info() =>
        client.Emit(type, new JsonObject { ["name"] = name, ["id"] = uuid });

    /// <summary>v1 event listener. "connect" fires on every (re)connection;
    /// other names are raw server events.</summary>
    public void on(string eventName, Delegate callback)
    {
        if (eventName is "connect" or "reconnect")
        {
            lock (gate)
                connectListeners.Add(callback);
            if (client.IsConnected)
                Invoke(callback);
            return;
        }
        client.On(eventName, payload =>
        {
            var parameters = callback.Method.GetParameters();
            if (parameters.Length == 0)
                callback.DynamicInvoke();
            else
                callback.DynamicInvoke(new object?[] { payload?.ToJsonString() });
        });
    }

    public void disconnect() => client.Disconnect();

    private void OnConnected()
    {
        send_identification_info();
        Delegate[] listeners;
        lock (gate)
            listeners = connectListeners.ToArray();
        foreach (var l in listeners)
            Invoke(l);
    }

    private static void Invoke(Delegate callback)
    {
        try
        {
            if (callback.Method.GetParameters().Length == 0)
                callback.DynamicInvoke();
            else
                callback.DynamicInvoke(new object?[] { null });
        }
        catch (Exception e)
        {
            Logger.error("connect listener failed", e);
        }
    }
}
