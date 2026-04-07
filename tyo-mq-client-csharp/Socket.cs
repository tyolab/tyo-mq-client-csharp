namespace TYO_MQ_CLIENT;

using SocketIOClient;

using System.Text.Json;

public class Socket {

    delegate void OnConnectHandler();
    delegate void MessageHandler(Dictionary<string,string> msgDict);
    delegate void ResponseHandler(SocketIOResponse response);

    public class SocketListener {
        public void on_connect() {
            Console.WriteLine("[Connected]");
        }

        public void on_disconnect() {
            Console.WriteLine("[Disconnected]");
        }

        public void on_message(object message) {
            Console.Write("[Message]: ");
            Console.WriteLine(message);
        }

        public void on_error(Exception e) {
            Console.Write("[Error]: ");
            Console.WriteLine(e);
        }

        public void on_reconnect() {
            Console.WriteLine("[Reconnected]");
        }
    }

    private SocketIO? socket;

    private SocketListener? defaultListener;

    public string name { get; set; }

    public string host { get; set; }
    public int port { get; set; }
    public string protocol { get; set; }

    public List<Delegate> on_connect_listeners { get; set; }

    public Delegate? on_error_listener { get; set; }

    public List<Delegate>? on_event_func_list { get; set; }

    public bool autoreconnect { get; set; }

    public string type { get; set; }

    public string? alias { get; set; }

    public string? uuid { get; set; }

    public string socket_id
    {
        get { return socket.Id; }
    }

    public SocketIO? RawSocket {
        get { return socket; }
    }

    public Delegate? listener { get; set; }

    public bool connected { get; set; }

    public bool is_socket_connected() {
        return connected && socket.Connected; 
    }

    public bool debug { get; set; }

    private List<OnAnyHandler>? pending_on_any_handlers { get; set; }

    // Tracks in-flight CONSUME_CHUNK transfers keyed by transferId
    private class ChunkTransfer {
        public string[] parts;
        public int received;
        public int total;
        public string eventName;
        public ChunkTransfer(int total, string eventName) {
            this.parts     = new string[total];
            this.received  = 0;
            this.total     = total;
            this.eventName = eventName;
        }
    }
    private Dictionary<string, ChunkTransfer> inboundChunks = new();

    // Local dispatch map: allows assembled chunks to reach registered callbacks
    // without going through the socket.io event system again
    protected Dictionary<string, Action<Dictionary<string, string>>> localHandlers = new();

    public Socket(string? host = null, int port = -1, string? protocol = null) {
        this.type = "RAW";

        this.autoreconnect = true;

        if (host == null)
            this.host = "localhost";
        else
            this.host = host;

        if (port <= 0)
            this.port = Constants.DEFAULT_PORT;
        else
            this.port = port;

        if (protocol != null)
            this.protocol = protocol;
        else
            this.protocol = "ws";

        socket = null;
        this.connected = false;
        this.debug = false;
        this.uuid = Guid.NewGuid().ToString();
        this.name = Constants.ANONYMOUS;
        this.alias = null;

        this.on_connect_listeners = new List<Delegate>();
        this.on_error_listener = null;
        this.on_event_func_list = null;
        this.pending_on_any_handlers = null;

        this.defaultListener = new SocketListener();
    }

    private void __apply_on_events (Dictionary<string,string> msgDict) {
        if (this.on_event_func_list != null && this.on_event_func_list.Count > 0) {
            if (this.on_event_func_list != null) {
                foreach (Delegate func in this.on_event_func_list) {
                    func.DynamicInvoke(new object[] { msgDict });
                }
                this.on_event_func_list.Clear();
                this.on_event_func_list = null;
            }
        }
    }

    private void __apply_pending_on_any_handlers() {
        if (this.pending_on_any_handlers != null && this.pending_on_any_handlers.Count > 0) {
            if (this.debug) {
                Logger.log($"[DEBUG] Applying {this.pending_on_any_handlers.Count} pending on_any handler(s)");
            }
            foreach (OnAnyHandler handler in this.pending_on_any_handlers) {
                if (socket != null) {
                    socket.OnAny(handler);
                }
            }
            this.pending_on_any_handlers.Clear();
            this.pending_on_any_handlers = null;
        }
    }

    public void send_identification_info() {
        this.send_message(this.type, $"{{\"name\": \"{ this.name }\", \"id\": \"{ this.uuid }\"}}");
    }

    public void on_connect(Dictionary<string,string>? msgDict = null) {
        Logger.log("connected to message queue server");

        if (this.debug) {
            Logger.log($"[DEBUG] Connection established - Socket ID: {this.uuid}");
        }

        this.connected = true;
        socket.On("ERROR", response => {
            __on_error__(_handle_response(response));
        });

        this.send_identification_info();

        // Apply any pending on_any handlers
        this.__apply_pending_on_any_handlers();

        if (this.debug) {
            Logger.log($"[DEBUG] Invoking {this.on_connect_listeners.Count} connect listener(s)");
        }

        int i = 0;
        while (i < this.on_connect_listeners.Count) {
            listener = this.on_connect_listeners[i];
            if (null == msgDict)
                listener.DynamicInvoke();
            else
                listener.DynamicInvoke(new object[] {msgDict});
            i += 1;
        }
    }

    public void on_disconnect(object message) {
        this.connected = false;
        inboundChunks.Clear();
        Logger.log("Socket (" + this.uuid + ") is disconnected", message);

        // The SocketIO client will automatically attempt reconnection
        // if Reconnection is enabled in Factory.Options
        if (this.autoreconnect) {
            Logger.log("Auto-reconnect is enabled, waiting for reconnection...");
        }
    }

    public void on_reconnect(object message) {
        Logger.log("Socket (" + this.uuid + ") is reconnecting...", message);

        // Re-establish connection state and send identification
        this.connected = true;
        this.send_identification_info();

        // Apply any pending on_any handlers (in case they were added during disconnection)
        this.__apply_pending_on_any_handlers();

        // Notify all connect listeners that we've reconnected
        int i = 0;
        while (i < this.on_connect_listeners.Count) {
            listener = this.on_connect_listeners[i];
            listener.DynamicInvoke();
            i += 1;
        }
    }

    public void on_error(object message) {
        Logger.error("oops, something wrong", message);
    }
        
    // #
    // # On TYO-MQ ERROR MESSAGE
    // #
    public void __on_error__(Dictionary<string, string> msg) {
        if (this.on_error_listener != null){
            this.on_error_listener.DynamicInvoke();
        }
        else{
            Logger.error("Error", msg);
        }
    }
    // #
    // #
    // #
    public async Task connect(Delegate? callback = null, int waittime = -1) {
        // # Example
        // # with SocketIO(this.host, this.port, SocketListener) as socketIO{
        // #     socketIO.emit("event")
        // #     socketIO.wait(seconds=1)
        string connectStr = this.host.StartsWith("http") ? this.host : this.protocol + "://" + this.host + ":" + this.port.ToString() + "/";

        if (this.debug) {
            Logger.log($"[DEBUG] Attempting to connect to: {connectStr}");
        }

        if (null == socket) {
            socket = new SocketIO(connectStr, Factory.Options);

            socket.On("disconnect", message => {
                this.on_disconnect(message);
            });
            socket.On("reconnect", message => {
                this.on_reconnect(message);
            });

            // Reassemble large messages split by the server into CONSUME_CHUNK frames
            socket.On("CONSUME_CHUNK", response => {
                try {
                    JsonElement root = response.GetValue<JsonElement>();

                    string transferId = root.GetProperty("transferId").GetString()!;
                    string chunkEvent = root.GetProperty("event").GetString()!;
                    int    index      = root.GetProperty("index").GetInt32();
                    int    total      = root.GetProperty("total").GetInt32();
                    string data       = root.GetProperty("data").GetString()!;

                    if (!inboundChunks.TryGetValue(transferId, out var transfer)) {
                        transfer = new ChunkTransfer(total, chunkEvent);
                        inboundChunks[transferId] = transfer;
                    }

                    transfer.parts[index] = data;
                    transfer.received++;

                    if (transfer.received == transfer.total) {
                        inboundChunks.Remove(transferId);
                        string fullJson = string.Join("", transfer.parts);
                        Dictionary<string, string>? assembled;
                        try {
                            assembled = JsonSerializer.Deserialize<Dictionary<string, string>>(fullJson);
                        }
                        catch (Exception ex) {
                            Logger.error("CONSUME_CHUNK: reassembly parse failed", ex);
                            return;
                        }
                        if (assembled != null && localHandlers.TryGetValue(chunkEvent, out var handler))
                            handler(assembled);
                    }
                }
                catch (Exception ex) {
                    Logger.error("CONSUME_CHUNK: processing error", ex);
                }
            });

            // Catch-all handler to see ALL incoming events (for debugging)
            socket.OnAny((eventName, response) => {
                if (this.debug) {
                    Logger.log($"[DEBUG] *** Event received from server: '{eventName}' ***");
                    try {
                        var msgDict = _handle_response(response);
                        Logger.log($"[DEBUG] *** Event '{eventName}' data:", msgDict);
                    }
                    catch (Exception ex) {
                        Logger.log($"[DEBUG] *** Event '{eventName}' raw response:", response);
                        Logger.log($"[DEBUG] *** Could not parse response:", ex.Message);
                    }
                }
            });

            socket.OnConnected += async (sender, e) =>
            {
                on_connect(null);
                if (null != callback)
                    callback.DynamicInvoke();
            };
        }

        if (socket.Connected) {
            if (this.debug) {
                Logger.log("[DEBUG] Socket already connected");
            }
            return;
        }

        await socket.ConnectAsync();

        if (this.debug) {
            Logger.log("[DEBUG] ConnectAsync() completed");
        }

        // if (duration == -1) 
        //     socket.wait();
        // else
        //     socket.wait(duration);
    }

    // public string get_id() {
    //     return this.id;
    // }

    public void add_on_connect_listener(Delegate listener) {
        this.on_connect_listeners.Add(listener);
    }

    public void disconnect() {
        if (socket != null && this.connected)
            socket.DisconnectAsync();
    }

    private Dictionary<string, string>? _handle_response(SocketIOResponse response) {
        if (this.debug) {
            Logger.log("[DEBUG] Raw response received:", response);
        }

        // Get the first data in the response
        object? message = null;
        Dictionary<string, string>? result = null;
        try {
            message = response.GetValue<string>();
            result = JsonSerializer.Deserialize<Dictionary<string, string>>((string)message);
            if (this.debug) {
                Logger.log("[DEBUG] Parsed message (string):", result);
            }
            return result;
        }
        catch (Exception ex) {
            // OK, it is not a string, could be an JSON object then
            try {
                message = response.GetValue<object>();
                if (null != message) {
                    result = JsonSerializer.Deserialize<Dictionary<string, string>>(message.ToString());
                    if (this.debug) {
                        Logger.log("[DEBUG] Parsed message (object):", result);
                    }
                    return result;
                }
            }
            catch (Exception ex_obj) {
                Logger.error("Failed to get the message", ex_obj);
            }
        }
        return null; // new Dictionary<string, string>();
    }

    public void on(string eventName, Delegate callback) {
        if (this.debug) {
            Logger.log($"[DEBUG] Registering event handler for: {eventName}");
        }

        // Keep a local copy for direct dispatch (used by CONSUME_CHUNK reassembly)
        localHandlers[eventName] = (dict) => callback.DynamicInvoke(new object[] { dict });

        if (socket == null ) {
            // #raise Exception("Socket is not created yet")
            if (this.on_event_func_list == null) {
                this.on_event_func_list = new List<Delegate>();
                // futureFunc = lambda : this.__apply_on_events();
                MessageHandler futureFunc = (Dictionary<string,string> msgDict) => this.__apply_on_events(msgDict);
                this.add_on_connect_listener(futureFunc);
            }

            this.on_event_func_list.Add(callback);
        }
        else
            socket.On(eventName, response => {
                if (this.debug) {
                    Logger.log($"[DEBUG] Event received: {eventName}");
                }
                var msgDict = _handle_response(response);
                callback.DynamicInvoke(new object[] { msgDict} );
            });
    }

    /**
     * Listen to ALL events from the server (catch-all)
     * Useful for debugging to see what events are being sent
     *
     * callback signature: void callback(string eventName, SocketIOResponse response)
     *
     * Can be called before connect() - handler will be registered when connection is established
     */
    public void on_any(OnAnyHandler callback) {
        if (this.debug) {
            Logger.log("[DEBUG] Registering catch-all event handler");
        }

        if (socket == null) {
            // Socket not initialized yet - queue the handler for later registration
            if (this.pending_on_any_handlers == null) {
                this.pending_on_any_handlers = new List<OnAnyHandler>();
            }
            this.pending_on_any_handlers.Add(callback);

            if (this.debug) {
                Logger.log("[DEBUG] Socket not ready, queuing on_any handler for later registration");
            }
        }
        else {
            // Socket is ready - register immediately
            socket.OnAny(callback);
        }
    }

    public void off(string? eventName = null) {
        if (eventName != null)
            localHandlers.Remove(eventName);

        if (socket == null) {

        }
        else {
            socket.Off(eventName);
        }
    }

    /**
     * Send message (in JSON) to the server
     */
    public void send_message(string eventName, string msg, Delegate? callback = null) {
        if (socket == null)
            throw new Exception("Socket isn't ininitalized yet");

        if (this.debug) {
            Logger.log($"[DEBUG] Sending message - Event: {eventName}, Message: {msg}");
        }

        ResponseHandler on_response = (response) => {
            Dictionary<string, string> msgDict = _handle_response(response);
            Logger.log("response", msgDict);
            if (callback != null) {
                callback.DynamicInvoke(new object[] { msgDict });
            }
        };

        if (!socket.Connected) {
            if (this.debug) {
                Logger.log($"[DEBUG] Socket not connected, queueing message for event: {eventName}");
            }

            OnConnectHandler futureFunc = () => socket.EmitAsync(eventName, on_response, msg);

            if (this.autoreconnect)
                this.connect(futureFunc, -1);
            else
                throw new Exception("Socket is created but not connected");
        }
        else {
            // socket.EmitAsync("PING", (response) => {
            //     Console.WriteLine(response);
            // }, "PING");
            socket.EmitAsync(eventName, (response) => {
                on_response.DynamicInvoke();
            }, msg);
        }
    }
}