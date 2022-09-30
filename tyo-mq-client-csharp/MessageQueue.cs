namespace TYO_MQ_CLIENT;
public class MessageQueue {
    private string host;

    private int port;

    private string protocol;

    public string Host 
    {
        get { return host; }
        set { host = value; }
    }

    public int Port 
    {
        get { return port; }
        set { port = value; }
    }

    public string Protocol 
    {
        get { return protocol; }
        set { protocol = value; }
    }

    public MessageQueue(string host, int port, string protocol) {
        // The SocketIO instance
        this.host = host;
        this.port = port;
        this.protocol = protocol;
    }          

    public Socket createSocket(string host, int port, string protocol) {
        Socket mySocket = new Socket(host != null ? host : this.host, port > 0 ? port : this.port, protocol != null ? protocol : this.protocol);
        return mySocket;
    }

    /**
     #* private function
     #*/
    private Subscriber __createConsumerPrivate(string name, string host, int port, string protocol) {
        Subscriber consumer = new Subscriber(name, host != null ? host : this.host, port > 0 ? port : this.port, protocol != null ? protocol : this.protocol);
        return consumer;
    }

    /**
     * Create a consumer
     */
    public Subscriber createConsumer(string name, string host, int port, string protocol) {
        return this.__createConsumerPrivate(name, host, port, protocol);
    }

    /**
     * Alias of createConsumer
     */

    public Subscriber createSubscriber(string name, string host, int port, string protocol) {
        return this.createConsumer(name, host, port, protocol);
    }

    private Publisher __createProducerPrivate (string name, string eventDefault, string host, int port, string protocol) {
        string h = host != null ? host : this.host;
        int p = port > 0 ? port : this.port;
        string ptc = protocol != null ? protocol : this.protocol;

        Publisher producer = new Publisher(name, eventDefault, h, p, ptc);
        return producer;
    }
     
    /**
     * Create a producer
     */
    public Publisher createProducer (string name, string eventDefault, string host, int port, string protocol) {
        return  this.__createProducerPrivate(name, eventDefault, host, port, protocol);
    }

    public Publisher createPublisher (string name, string eventDefault, string host, int port, string protocol) {
        return this.createProducer(name, eventDefault, host, port, protocol);
    }
}