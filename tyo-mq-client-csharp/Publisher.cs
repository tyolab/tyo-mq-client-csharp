namespace tyo_mq_client_csharp;

using System.Text.Json;
using System.Text.Json.Serialization;

public class Publiser: Subscriber {

    private Dictionary<string, Subscriber> subscribers;

    public Publiser(string name, string eventDefault, string host, int port, string protocol) : base(name, host, port, protocol) {

        this.type = "PRODUCER";
        this.eventDefault = eventDefault != null? eventDefault : Constants.EVENT_DEFAULT;
        this.on_subscription_listener = null;
        this.subscribers = new Dictionary<string, Subscriber>();

        // Initialisation
        // futureFunc = lambda : this.set_on_subscription_listener()
        // this.add_on_connect_listener(futureFunc)

        Logger.debug("creating producer: " + this.name);
    }

    public void broadcast (string data,  string eventName) {
        this.produce(data,  eventName, Constants.METHOD_BROADCAST);
    }
        
    public void produce (string data, string eventName, string method) { 
        if (data == null)
            return;
        
        if (eventName == null) {
            if (this.eventDefault == null) {
                throw new Exception("please specifiy eventName");
            }
            else {
                 eventName = this.eventDefault;
            }
        }

        // for C#10 (dotnet 6.0) use:
        string message = "{'event':" + eventName + ", 'message': " + data + ", 'from': " + this.name + ", method': " + method + "}";
        Logger.debug("sending message: " + message);
        this.send_message("PRODUCE", message);
    }

    /**
     * On Subscribe
     */
    public void __on_subscription (object data)  {
        Logger.log("Received subscription information: " + JsonSerializer.Serialize(data));

        this.subscribers[data["id"]] = data;

        // further listener
        if (this.on_subscription_listener != null) 
            this.on_subscription_listener(data);
    }

    public void set_on_subscription_listener () {
        String eventName = Events.to_onsubscribe_event(this.get_id());
        this.on(eventName, this.__on_subscription);
    }

    /**
     * On Lost connections with subscriber(s)
     */
    public void __on_lost_subscriber (Delegate callback, object data)  {
        Logger.log("Lost subscriber\"s connection");
        if (callback is not null) {
            callback.DynamicInvoke(new object[] { data });
        }
    }

    public void set_on_subscriber_lost_listener (Delegate callback)  {
        string eventName = Events.to_ondisconnect_event(this.get_id());
        // futureFunc = lambda data : (lambda data, cb=callback : this.__on_lost_subscriber(cb, data))(data)
        this.on(eventName, callback);
    }

    public void on_subscriber_lost (Delegate callback) {
        this.set_on_subscriber_lost_listener(callback);
    }

    /**
     * On Unsubsribe
     */
    public void __on_unsubscribed (Delegate callback, object data) {
        if (callback is not null) {
           callback.DynamicInvoke(new object[] { data });
        }
    }

    public void set_on_unsubscribed_listener (string eventName, Delegate callback)  {
        string eventName = Events.to_onunsubscribe_event(eventName, this.get_id());
        // futureFunc = lambda data : (lambda data, cb=callback: this.__on_unsubscribed(cb, data))(data)
        this.on(eventName, callback);
    }

    public void on_unsubscribed (string eventName, Delegate callback)  {
        this.set_on_unsubscribed_listener(eventName, callback); 
    }
}
