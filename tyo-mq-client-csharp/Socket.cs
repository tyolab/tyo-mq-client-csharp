namespace tyo_mq_client_csharp;

using SocketIOClient;

public class Socket {

    delegate void SocketOp();
    delegate void SocketResponse(object response);

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

    // private Guid guid = Guid.NewGuid();

    public Socket(string host, int port, string protocol) {
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
            this.protocol = "http";

        this.socket = null;
        this.connected = false;
        this.id = Guid.NewGuid().ToString();
        this.name = Constants.ANONYMOUS;
        this.alias = null;

        this.on_connect_listeners = new List<Delegate>();
        this.on_error_listener = null;
        this.on_event_func_list = null;

        this.defaultListener = new SocketListener();
    }

    private void __apply_on_events () {
        if (this.on_event_func_list != null && this.on_event_func_list.Length > 0) {
            // map(lambda func : func(), this.on_event_func_list)
            // del this.on_event_func_list
            if (this.on_event_func_list != null) {
                foreach (Delegate func in this.on_event_func_list) {
                    func();
                }
                this.on_event_func_list.Clear();
                this.on_event_func_list = null;
            }
        }
    }

    public void send_identification_info() {
        this.send_message(this.type, "{'name': " + this.name + ", 'id': " + this.get_id() + "}");
    }

    public void on_connect(object response) {
        Logger.log("connected to message queue server", response);

        this.connected = true;
        this.socket.On("ERROR", this.__on_error__);

        this.send_identification_info();

        i = 0;
        while (i < len(this.on_connect_listeners)) {
            listener = this.on_connect_listeners[i]; 
            listener();
            i += 1;
        }
    }

    public void on_disconnect(object message) {
        this.connected = false;
        // #Logger.debug('disconnect')
        Logger.log("Socket (" + this.get_id() + ") is disconnected", message);
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
    public void __on_error__(object msg) {
        if (this.on_error_listener != null){
            this.on_error_listener();
        }
        else{
            Logger.error("Error", msg);
        }
    }
    // #
    // #
    // #
    public void connect(Delegate callback, int duration = -1) {
        // # Example
        // # with SocketIO(this.host, this.port, SocketListener) as socketIO{
        // #     socketIO.emit("event")
        // #     socketIO.wait(seconds=1)
        string connectStr = this.host.StartsWith("http") ? this.host : this.protocol + "://" + this.host + ":" + this.port.ToString() + "/";

        this.socket = new SocketIO(connectStr);
        this.socket.On("connect", response => {
            this.on_connect(response);
            if (callback != null) {
                callback(response);
            }
        });
        this.socket.On("disconnect", message => {
            this.on_disconnect(message);
        });
        this.socket.On("reconnect", message => {
            this.on_reconnect(message);
        });

        // if (duration == -1) 
        //     this.socket.wait();
        // else
        //     this.socket.wait(duration);
    }

    // public string get_id() {
    //     return this.id;
    // }

    public void add_on_connect_listener(Delegate listener) {
        this.on_connect_listeners.add(listener);
    }

    public void disconnect() {
        if (this.socket != null && this.connected)
            this.socket.disconnect();
    }

    public void on(string eventName, Delegate callback) {
        if (this.socket == null ) {
            // #raise Exception("Socket is not created yet")
            if (this.on_event_func_list == null) {
                this.on_event_func_list = new List<Delegate>();
                // futureFunc = lambda : this.__apply_on_events();
                SocketOp futureFunc = () => this.__apply_on_events();
                this.add_on_connect_listener(futureFunc);
            }

            this.on_event_func_list.add(callback);
        }
        else
            this.socket.On(eventName, callback);
    }

    public void send_message(string eventName, string msg){
        if (this.socket == null)
            throw new Exception("Socket isn't ininitalized yet");
        
        SocketResponse onResponse = (response) => {
            Logger.log("response", response);
        };

        if (!this.socket.connected) {
            // futureFunc = lambda eventName,msg: this.socket.EmitAsync(eventName,msg)
            SocketOp futureFunc = () => this.socket.EmitAsync(eventName, onResponse, msg);

            if (this.autoreconnect)
                this.connect(-1, futureFunc);
            else
                throw new Exception("Socket is created but not connected");
        }
        else
            this.socket.emit(eventName, onResponse, msg);
    }
}