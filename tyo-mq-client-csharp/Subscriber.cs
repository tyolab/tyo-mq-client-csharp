namespace TYO_MQ_CLIENT;

using System.Text.Json;
using System.Text.Json.Serialization;
public class Subscriber: Socket {

    delegate void SubscriptionEventHandler(/* string eventName, string who, string name, string message */);
    delegate void OnTrySubscriptionHandler();

    delegate void OnNewMessageHandler(Dictionary<string, string> data/* string eventName, string message */);

    delegate void OnResubscribeListener();

    private List<Delegate>? subscriptions;

    private Dictionary<string, Delegate>? consumes;

    public Subscriber(string name, string? host = null, int port = -1, string? protocol = null) : base(host, port, protocol) {

        this.type = "CONSUMER";
        this.name = name != null? name : Constants.ANONYMOUS;
        this.consumes = null; // new Dictionary<string, Delegate>();
        this.subscriptions = null; // new List<Delegate>();

        Logger.debug("creating subscriber: " + this.name);
    }

    private void __apply_subscriptions() {
        if (this.__apply_subscriptions != null && 
            this.subscriptions != null &&
            this.subscriptions.Count > 0) {
            if (this.subscriptions != null) {
                foreach (Delegate func in this.subscriptions) {
                    func.DynamicInvoke();
                }
                this.subscriptions.Clear();
                this.subscriptions = null;
            }
        }
    }

    private void __trigger_consume_event(Dictionary<string, string> obj, string eventStr, Delegate? callback)  {
        // if obj["eventName"] == eventStr 
        object? data = null != obj ? obj["message"] : null;
        if (callback != null) {
            if (null == data)
                callback.DynamicInvoke();
            else
                callback.DynamicInvoke(new object[] { data });
        }
    }

    // For debug
    
     private void __debug_on_message(string eventName, Dictionary<string, string> message) {
        string messageJsonStr = JsonSerializer.Serialize(message);
        Logger.debug("received message", eventName, messageJsonStr);
        if (null != this.consumes)
            try {
                Delegate func = this.consumes[eventName]; 
                if (null != func)
                    this.__trigger_consume_event(message, eventName, func);
            }
            catch (Exception e) {
                Logger.error("Ooops, something wrong", e);
            }
        // Logger.debug(eventName, ":", JsonSerializer.Serialize(message));
        // callback(message)
    }

     private void __debug_on_message(params string[] args) {
        Logger.debug("received message", args);
     }

     private void __subscribe_internal(string who, string? eventName = null, Delegate? onConsumeCallback = null) {    
        string eventStr;
        bool is_all = false;
        if (string.IsNullOrEmpty(eventName)) {
            eventStr = Constants.EVENT_ALL; // Events.to_consumer_all_event(who); // who + "-ALL";
            is_all = true;
        }
        else
            eventStr = Events.to_event_string(eventName);

        /**
            * @todo
            * 
            * deal with the ALL events later
            */
        string scope_str = is_all ? Constants.SCOPE_ALL : Constants.SCOPE_DEFAULT;
        string messageStr = $"{{\"event\": \"{eventStr}\", \"producer\": \"{who}\", \"consumer\": \"{name}\", \"scope\": \"{scope_str}\"}}";

        SubscriptionEventHandler sendNewSubscriptionMessage = () => { 
            send_message("SUBSCRIBE", messageStr, onConsumeCallback);
        };

        // On Connect Message will be trigger by system
        if (this.connected) 
            sendNewSubscriptionMessage();
        else {
            if (this.subscriptions == null) {
                this.subscriptions = new List<Delegate>();
                OnTrySubscriptionHandler trySubscription = () => {
                    this.__apply_subscriptions();
                };
                this.add_on_connect_listener(trySubscription);
            }

            this.subscriptions.Add(sendNewSubscriptionMessage);
        }
        // the connection should be ready before we subscribe the message
        // this.on('connect', function ()  {
        //     sendSubscriptionMessage()
        // })

        if (this.consumes == null)
            this.consumes = new Dictionary<string, Delegate>();

        string consumerEventStr = Events.to_consumer_event(eventStr, who, is_all);
        if (onConsumeCallback != null) {
            this.consumes[consumerEventStr] = onConsumeCallback;
        }
         // #lambda message, fromWhom : onConsumeCallback(message, fromWhom)
        // #lambda obj : lambda obj, eventName=eventStr, callback=onConsumeCallback : this.__trigger_consume_event(obj, eventName, callback)

        // #futureFunc = lambda data : (lambda data, eventName=consumeEventStr: this.consumes[eventName](data))(data)
        // #futureFunc = lambda data, eventStr=consumeEventStr : this.consumes[eventStr](data)
        // #DEBUG
        Logger.debug("setting on eventName: " + consumerEventStr);
        // #this.on(consumeEventStr, this.__debug_on_message)
        // #futureFunc = lambda data : this.__debug_on_message(data)
        OnNewMessageHandler futureFunc = (data) => {
            __debug_on_message(consumerEventStr, data);
        };
        // futureFunc = lambda data, eventName=consumeEventStr : this.__debug_on_message(eventName, data)
        string consumeEventStr = Events.to_consume_event(consumerEventStr);
        this.off(consumeEventStr);
        this.on(consumeEventStr, futureFunc);
     }

     public void resubscribeWhenReconnect(string who, string? eventName = null, Delegate? onConsumeCallback = null, bool reSubscribe = true) {

        this.__subscribe_internal(who, eventName, onConsumeCallback);

        if (reSubscribe) {
            // resubscribeListener = lambda who=who, eventStr=eventName, callback=onConsumeCallback: this.__subscribe_internal(who, eventStr, callback)
            OnResubscribeListener resubscribeListener = () => {
                this.__subscribe_internal(who, eventName, onConsumeCallback);
            };

            this.add_on_connect_listener(resubscribeListener);
        }
     }

    /**
     * Subscribe message
     * 
     * If an eventName name is not provided, then we subscribe all the messages from the producer
     */

    public void subscribe (string who, string? eventName = null, Delegate?onConsumeCallback = null, bool reconcect = true) {
        this.resubscribeWhenReconnect(who, eventName, onConsumeCallback, reconcect);
    }

    /**
     */

    public void subscribe (string who, Delegate?onConsumeCallback = null, bool reconcect = true) {
        subscribe(who, null, onConsumeCallback, reconcect);
     }

    /**
     * Subscribe only once, if the connection is gone, let it be
     */

     public void subscribeOnce (string who, string? eventName = null, Delegate? onConsumeCallback = null) {
        this.subscribe(who, eventName, onConsumeCallback, false);
     }

    /**
     * Subscribe all events with this name whatever providers are publishing
     */
     public void subscribeAll (string who, Delegate? onConsumeCallback = null) {
        this.subscribe(who, null, onConsumeCallback, true);
    }

    /**
     */
    public async Task register(Delegate? callback = null, int waittime = -1) {
        await connect(callback, waittime);
    }
}
