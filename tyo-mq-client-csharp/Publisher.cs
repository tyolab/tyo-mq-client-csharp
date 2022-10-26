using System.Text.Json;

namespace TYO_MQ_CLIENT;
/**
 * Publisher
 * =========
 * Publisher could maintain a copy of the subscription list
 * - types:
 *         * free
 *         * paid membership
 *         
 *   subscription:
 *         * name
 *         * type
 *         * id
 *         * token  // secret, for authentication
 */
 
public class Publisher: Subscriber {

    private Dictionary<string, Subscription> subscribers;

    private string? eventDefault = null;

    private Delegate? on_subscription_listener;

    public Publisher(string name, string? eventDefault = null, string? host = null, int port = -1, string? protocol = null) : base(name, host, port, protocol) {

        this.type = "PRODUCER";
        this.eventDefault = string.IsNullOrEmpty(eventDefault) ? eventDefault : 
                                        (string.IsNullOrEmpty(this.eventDefault) ? 
                                            this.eventDefault: $"{name}-{Constants.EVENT_DEFAULT}");
        this.on_subscription_listener = null;
        this.subscribers = new Dictionary<string, Subscription>();

        // Initialisation
        // futureFunc = lambda : this.set_on_subscription_listener()
        // this.add_on_connect_listener(futureFunc)

        Logger.debug("creating producer: " + this.name);
    }

    /**
     * 
     */
        
    public void produce (string data, string? eventName = null, string? method = null) { 
        if (data == null)
            return;
        
        if (string.IsNullOrEmpty(eventName)) {
            if (this.eventDefault == null) {
                throw new Exception("please specifiy eventName");
            }
            else {
                 eventName = this.eventDefault;
            }
        }

        // for C#10 (dotnet 6.0) use:
        string message = $"{{\"event\": \"{ eventName }\", \"message\": \"{ data }\", \"from\": \"{ this.name }\", \"method\": \"{ (method ?? Constants.METHOD_BROADCAST) }\"}}";
        Logger.debug("sending message: " + message);
        this.send_message("PRODUCE", message);
    }

    /**
     * On Subscribe
     */
    private void __on_subscription (Dictionary<string, string> data)  {
        Logger.log("Received subscription information: " + JsonSerializer.Serialize(data));

        /**
         * @todo
         * maybe we only need to keep a copy of the subscription
         * like name of the subcriber {
            *  "name": "name",
            *  "id": ID,   // socket id
            *  ...
         }
         */
        this.subscribers.Add(data["id"], new Subscription(data["id"]));

        // further listener
        if (this.on_subscription_listener != null) 
            this.on_subscription_listener.DynamicInvoke(new object[] { data });
    }

    public void set_on_subscription_listener () {
        String eventName = Events.to_onsubscribe_event(this.id);
        this.on(eventName, this.__on_subscription);
    }

    /**
     * On Lost connections with subscriber(s)
     */
    public void __on_lost_subscriber (Delegate callback, object data)  {
        Logger.log("Lost subscriber\"s connection");
        if (callback != null) {
            callback.DynamicInvoke(new object[] { data });
        }
    }

    public void set_on_subscriber_lost_listener (Delegate callback)  {
        string eventName = Events.to_ondisconnect_event(this.id);
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
        if (callback != null) {
           callback.DynamicInvoke(new object[] { data });
        }
    }

    public void set_on_unsubscribed_listener (string eventName, Delegate callback)  {
        eventName = Events.to_onunsubscribe_event(eventName, this.id);
        // futureFunc = lambda data : (lambda data, cb=callback: this.__on_unsubscribed(cb, data))(data)
        this.on(eventName, callback);
    }

    public void on_unsubscribed (string eventName, Delegate callback)  {
        this.set_on_unsubscribed_listener(eventName, callback); 
    }
}
