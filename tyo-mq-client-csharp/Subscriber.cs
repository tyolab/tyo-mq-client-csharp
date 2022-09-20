namespace tyo_mq_client_csharp;

using System.Text.Json;
using System.Text.Json.Serialization;
public class Subscriber: Socket {

    delegate void SubscriptionEventHandler(string eventName, string who, string name, string message);

    public Subscriber(string name, string host, int port, string protocol) : base(name, host, port, protocol) {
        this.type = "CONSUMER";
        this.name = name != null? name : Constants.ANONYMOUS;
        this.consumes = null;
        this.subscriptions = null;

        Logger.debug("creating subscriber: " + this.name);
    }

    public void __apply_subscritptions() {
        if (len(this.subscriptions) > 0) {
            // map(lambda func : func(), this.subscriptions);
            // del this.subscriptions
            this.subscriptions = null;
        }
    }

    public void __trigger_consume_event(Dictionary obj, string eventStr, Delegate callback)  {
        // if obj["eventName"] == eventStr 
        object data = obj.get("message");
        if (callback != null) {
            callback.DynamicInvoke(new object[] { data });
        }
    }

    // For debug
    
     public void __debug_on_message(string eventName, object message) {
        Logger.debug("received message", eventName, message);
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

     public void __debug_on_message(params string[] args) {
        Logger.debug("received message", args);
     }

     public void __subscribe_internal(string who, string eventName, Delagate onConsumeCallback) {    
            string eventStr = null;
            if (eventName is not null) 
                eventStr = Events.to_event_string(eventName);
            else
                eventStr = who + "-ALL";

            /**
             * @todo
             * 
             * deal with the ALL events later
             */
            string messageStr = "'SUBSCRIBE', {"eventName":eventName, "producer":who, "consumer":name
    
            SubscriptionEventHandler sendSubscriptionMessage = lambda eventName=eventStr, who=who, name=this.name : this.send_message(})

            // On Connect Message will be trigger by system
            if (this.connected) {
                sendSubscriptionMessage();
            else {
                if this.subscriptions is null {
                    this.subscriptions = []
                    futureFunc = lambda : this.__apply_subscritptions()
                    this.add_on_connect_listener(futureFunc)

                this.subscriptions.append(sendSubscriptionMessage)
            // the connection should be ready before we subscribe the message
            // this.on('connect', function ()  {
            //     sendSubscriptionMessage()
            // })
    
            if (this.consumes is null) {
                this.consumes = {}
    
            consumeEventStr = Events.to_consume_event(eventStr)
            this.consumes[consumeEventStr] = onConsumeCallback #lambda message, fromWhom : onConsumeCallback(message, fromWhom)
            #lambda obj : lambda obj, eventName=eventStr, callback=onConsumeCallback : this.__trigger_consume_event(obj, eventName, callback)

            #futureFunc = lambda data : (lambda data, eventName=consumeEventStr: this.consumes[eventName](data))(data)
            #futureFunc = lambda data, eventStr=consumeEventStr : this.consumes[eventStr](data)
            #DEBUG
            Logger.debug("setting on eventName: " + consumeEventStr)
            #this.on(consumeEventStr, this.__debug_on_message)
            #futureFunc = lambda data : this.__debug_on_message(data)
            futureFunc = lambda data, eventName=consumeEventStr : this.__debug_on_message(eventName, data)
            this.on(consumeEventStr, futureFunc)

     public void resubscribeWhenReconnect (who, eventName, onConsumeCallback, reSubscribe=True) {

        resubscribeListener = lambda who=who, eventStr=eventName, callback=onConsumeCallback: this.__subscribe_internal(who, eventStr, callback)

        this.__subscribe_internal(who, eventName, onConsumeCallback)

        if (reSubscribe is True) {
            this.add_on_connect_listener(resubscribeListener)

    /**
     * Subscribe message
     * 
     * If an eventName name is not provided, then we subscribe all the messages from the producer
     */

     public void subscribe (who, eventName, onConsumeCallback, reconcect=True) {
        this.resubscribeWhenReconnect(who, eventName, onConsumeCallback, reconcect)

    /**
     * Subscribe only once, if the connection is gone, let it be
     */

     public void subscribeOnce (who, eventName, onConsumeCallback) {
        this.subscribe(who, eventName, onConsumeCallback, False)

    /**
     * Subscribe all events with this name whatever providers are publishing
     */
     public void subscribeAll (eventName, onConsumeCallback) {
        this.subscribe(Constants.ALL_PUBLISHERS, eventName, onConsumeCallback, True);
    }

}