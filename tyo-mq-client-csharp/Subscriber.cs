namespace tyo_mq_client_csharp;

using System.Text.Json;
using System.Text.Json.Serialization;
public class Subscriber: Socket {

    delegate void SubscriptionEventHandler(string eventName, string who, string name, string message);
    delegate void OnNewSubscriptionHandler();

    delegate void OnNewMessageHandler(string eventName, string message);

    delegate void OnResubscribeListener();

    private List<Delegate>? subscriptions;

    private Dictionary<string, Delegate>? consumes;

    public Subscriber(string name, string host, int port, string protocol) : base(host, port, protocol) {
        this.type = "CONSUMER";
        this.name = name != null? name : Constants.ANONYMOUS;
        this.consumes = null; // new Dictionary<string, Delegate>();
        this.subscriptions = null; // new List<Delegate>();

        Logger.debug("creating subscriber: " + this.name);
    }

    private void __apply_subscritptions() {
        if (this.__apply_subscritptions != null && this.subscriptions.Count > 0) {
            // map(lambda func : func(), this.subscriptions);
            // del this.subscriptions
            if (this.subscriptions != null) {
                foreach (Delegate func in this.subscriptions) {
                    func.DynamicInvoke();
                }
                this.subscriptions.Clear();
                this.subscriptions = null;
            }
        }
    }

    private void __trigger_consume_event(Dictionary<string, object> obj, string eventStr, Delegate callback)  {
        // if obj["eventName"] == eventStr 
        object data = obj["message"];
        if (callback != null) {
            callback.DynamicInvoke(new object[] { data });
        }
    }

    // For debug
    
     private void __debug_on_message(string eventName, object message) {
        Logger.debug("received message", eventName, message);
        if (null != this.consumes)
            try {
                func = this.consumes[eventName];
                this.__trigger_consume_event(message, eventName, func);
            }
            catch (Exception e) {
                Logger.error("Ooops, something wrong", e);
            }
        Logger.debug(eventName, ":", JsonSerializer.Serialize(message));
        // callback(message)
    }

     private void __debug_on_message(params string[] args) {
        Logger.debug("received message", args);
     }

     private void __subscribe_internal(string who, string eventName, Delegate onConsumeCallback) {    
            string eventStr = null;
            if (eventName != null) 
                eventStr = Events.to_event_string(eventName);
            else
                eventStr = who + "-ALL";

            /**
             * @todo
             * 
             * deal with the ALL events later
             */
            string messageStr = "{'eventName':" + eventName + ", 'producer': " + who + ", 'consumer': " + name + "}";
    
            SubscriptionEventHandler sendSubscriptionMessage = () => { 
                this.send_message("SUBSCRIBE", messageStr);
            };

            // On Connect Message will be trigger by system
            if (this.connected) 
                sendSubscriptionMessage();
            else {
                if (this.subscriptions == null) {
                    this.subscriptions = new List<Delegate>();
                    OnNewSubscriptionHandler onNewSubscription = () => {
                        this.__apply_subscritptions();
                    };
                    this.add_on_connect_listener(onNewSubscription);
                }

                this.subscriptions.add(sendSubscriptionMessage);
            }
            // the connection should be ready before we subscribe the message
            // this.on('connect', function ()  {
            //     sendSubscriptionMessage()
            // })
    
            if (this.consumes == null)
                this.consumes = new Dictionary<string, Delegate>();
    
            string consumeEventStr = Events.to_consume_event(eventStr);
            this.consumes[consumeEventStr] = onConsumeCallbackl; // #lambda message, fromWhom : onConsumeCallback(message, fromWhom)
            // #lambda obj : lambda obj, eventName=eventStr, callback=onConsumeCallback : this.__trigger_consume_event(obj, eventName, callback)

            // #futureFunc = lambda data : (lambda data, eventName=consumeEventStr: this.consumes[eventName](data))(data)
            // #futureFunc = lambda data, eventStr=consumeEventStr : this.consumes[eventStr](data)
            // #DEBUG
            Logger.debug("setting on eventName: " + consumeEventStr);
            // #this.on(consumeEventStr, this.__debug_on_message)
            // #futureFunc = lambda data : this.__debug_on_message(data)
            OnNewMessageHandler futureFunc = (object data) => {
                this.__debug_on_message(consumeEventStr, data);
            };
            // futureFunc = lambda data, eventName=consumeEventStr : this.__debug_on_message(eventName, data)
            this.on(consumeEventStr, futureFunc);
     }

     public void resubscribeWhenReconnect(string who, string eventName, Delegate onConsumeCallback, bool reSubscribe = true) {

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

     public void subscribe (string who, string eventName, Delegate onConsumeCallback, bool reconcect = true) {
        this.resubscribeWhenReconnect(who, eventName, onConsumeCallback, reconcect);
     }

    /**
     * Subscribe only once, if the connection is gone, let it be
     */

     public void subscribeOnce (string who, string eventName, Delegate onConsumeCallback) {
        this.subscribe(who, eventName, onConsumeCallback, false);
     }

    /**
     * Subscribe all events with this name whatever providers are publishing
     */
     public void subscribeAll (string eventName, Delegate onConsumeCallback) {
        this.subscribe(Constants.ALL_PUBLISHERS, eventName, onConsumeCallback, true);
    }

}
