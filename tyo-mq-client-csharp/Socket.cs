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

    public string? id { get; set; }

    public Delegate? listener { get; set; }

    public bool connected { get; set; }

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
        this.id = Guid.NewGuid().ToString();
        this.name = Constants.ANONYMOUS;
        this.alias = null;

        this.on_connect_listeners = new List<Delegate>();
        this.on_error_listener = null;
        this.on_event_func_list = null;

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

    public void send_identification_info() {
        this.send_message(this.type, $"{{\"name\": \"{ this.name }\", \"id\": \"{ this.id }\"}}");
    }

    public void on_connect(Dictionary<string,string>? msgDict = null) {
        Logger.log("connected to message queue server");

        this.connected = true;
        socket.On("ERROR", response => {
            __on_error__(_handle_response(response));
        });

        this.send_identification_info();

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
        // #Logger.debug('disconnect')
        Logger.log("Socket (" + this.id + ") is disconnected", message);
    }

    public void on_reconnect(object message) {
        Logger.debug("reconnect", message); 
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

        if (null == socket) {
            socket = new SocketIO(connectStr, Factory.Options);
            // socket.On("connect", response => {
            //     Dictionary<string, string> msgDict = _handle_response(response);
            //     this.on_connect(msgDict);
            //     if (callback != null) {
            //         callback.DynamicInvoke(new object[] { msgDict } );
            //     }
            // });
            // socket.On("disconnect", message => {
            //     this.on_disconnect(message);
            // });
            // socket.On("reconnect", message => {
            //     this.on_reconnect(message);
            // });
        }

        socket.OnConnected += async (sender, e) =>
        {
            on_connect(null);
            if (null != callback)
                callback.DynamicInvoke();
        };
        if (socket.Connected)
            return;
        await socket.ConnectAsync();

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
        Console.WriteLine(response);
    
        // Get the first data in the response
        object? message = null;
        try {
            message = response.GetValue<string>();
            return JsonSerializer.Deserialize<Dictionary<string, string>>((string)message);
        }
        catch (Exception ex) {
            // OK, it is not a string, could be an JSON object then
            try {
                message = response.GetValue<object>();
                if (null != message)
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(message.ToString());
            }
            catch (Exception ex_obj) {
                Logger.error("Failed to get the message", ex_obj);
            }
        }
        return null; // new Dictionary<string, string>();
    }

    public void on(string eventName, Delegate callback) {
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
                var msgDict = _handle_response(response);
                callback.DynamicInvoke(new object[] { msgDict} );
            });
    }

    public void off(string? eventName = null) {
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
        
        ResponseHandler on_response = (response) => {
            Dictionary<string, string> msgDict = _handle_response(response);
            Logger.log("response", msgDict);
            if (callback != null) {
                callback.DynamicInvoke(new object[] { msgDict });
            }
        };

        if (!socket.Connected) {
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