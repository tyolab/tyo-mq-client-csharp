namespace tyo_mq_client_csharp;

public class Socket {

    public class SocketListener {
        public void on_connect() {
            Console.WriteLine("[Connected]");
        }

        public void on_disconnect() {
            Console.WriteLine("[Disconnected]");
        }

        public void on_message(string message) {
            Console.WriteLine("[Message]: " + message);
        }

        public void on_error(Exception e) {
            Console.WriteLine("[Error]: " + e);
        }

        public void on_reconnect() {
            Console.WriteLine("[Reconnected]");
        }
    }

    public string name { get; set; }

    public string host { get; set; }
    public int port { get; set; }
    public string protocol { get; set; }

    public SocketListener[] on_connect_listeners { get; set; }

    public SocketListener on_error_listener { get; set; }

    public SocketListener[] on_event_func_listener { get; set; }

    public bool autoreconnect { get; set; }

    public string type { get; set; }

    public string alias { get; set; }

    public string id { get; set; }


    public Socket(string host, int port, string protocol, SocketListener listener) {
        this.type = "RAW";

        this.listener = listener;
        this.autoreconnect = true;

        if (host is null)
            this.host = "localhost";
        else
            this.host = host;

        if (port is null)
            this.port = Constants.DEFAULT_PORT;
        else
            this.port = port;

        if (protocol is not null)
            this.protocol = protocol;

        this.socket = null;
        this.connected = false;
        this.id = str(uuid.uuid4());
        this.name = Constants.ANONYMOUS;
        this.alias = null;

        this.on_connect_listeners = new List<SocketListener>();
        this.on_error_listener = null;
        this.on_event_func_list = null;
    }

    public void __apply_on_events () {
        if (this.on_event_func_list is not null && len(this.on_event_func_list) > 0) {
            // map(lambda func : func(), this.on_event_func_list)
            // del this.on_event_func_list
            this.on_event_func_list = null;
        }
    }

    public void send_identification_info() {
        this.send_message(this.type, "{'name': " + this.name + ", 'id': " + this.get_id() + "}");
    }

    public void on_connect() {
        Logger.log("connected to message queue server");

        this.connected = true;
        this.socket.on("ERROR", this.__on_error__);

        this.send_identification_info();

        i = 0;
        while (i < len(this.on_connect_listeners)) {
            listener = this.on_connect_listeners[i]; 
            listener();
            i += 1;
        }
    }

    public void on_disconnect() {
        this.connected = false;
        // #Logger.debug('disconnect')
        Logger.log("Socket (" + this.get_id() + ") is disconnected");
    }

    public void on_reconnect() {
        Logger.debug("reconnect"); 
    }

    public void on_error() {
        Logger.error("oops, something wrong.");
    }
        
    // #
    // # On TYO-MQ ERROR MESSAGE
    // #
    public void __on_error__(string msg) {
        if (this.on_error_listener is not null){
            this.on_error_listener();
        }
        else{
            Logger.error(msg);
        }
    }
    // #
    // #
    // #
    public void connect(int duration=-1, Delegate callback, SocketListener cls) {
        // # Example
        // # with SocketIO(this.host, this.port, SocketListener) as socketIO{
        // #     socketIO.emit("event")
        // #     socketIO.wait(seconds=1)
        this.socket = SocketIO(this.host, this.port, cls);
        this.socket.on("connect", this.on_connect is not null ? this.on_connect : callback);
        this.socket.on("disconnect", this.on_disconnect);
        this.socket.on("reconnect", this.on_reconnect);

        if (duration == -1) 
            this.socket.wait();
        else
            this.socket.wait(seconds=duration);
    }

    public string get_id() {
        return this.id;
    }

    public void add_on_connect_listener(listener) {
        this.on_connect_listeners.append(listener);
    }

    public void disconnect() {
        if (this.socket is not null and this.connected)
            this.socket.disconnect();
    }

    public void on(string eventName, Delegate callback) {
        if (this.socket is null ) {
            // #raise Exception("Socket is not created yet")
            if (this.on_event_func_list is null) {
                this.on_event_func_list = new List<SocketListener>();
                // futureFunc = lambda : this.__apply_on_events();
                // this.add_on_connect_listener(futureFunc);
            }

            this.on_event_func_list.append(callback);
        }
        else
            this.socket.on(eventName,callback);
    }

    public void send_message(string eventName, string msg){
        if (this.socket is null)
            throw new Exception("Socket isn't ininitalized yet");

        if (this.socket.connected is false) {
            // futureFunc = lambda eventName,msg: this.socket.emit(eventName,msg)

            if (this.autoreconnect is True)
                this.connect(-1, futureFunc);
            else
                throw new Exception("Socket is created but not connected");
        }
        else
            this.socket.emit(eventName,msg);
    }
}