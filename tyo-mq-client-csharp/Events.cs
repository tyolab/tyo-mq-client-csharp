namespace TYO_MQ_CLIENT;

public static class Events {

    // event is a keyword in C#
    public static string to_event_string(string event_name, string? prefix = null, string? suffix = null) {
        
        // eventStr = event
        // TODO
        // if (typeof event === "string") {
        //     eventStr = event
        // }
        // else if (typeof event === "object" && event.event) {
        //     eventStr = event.event
        // }
        // else 
        //     throw new Error ("Unknown event object: should be a string or object with event string")
        // return eventStr
        return (null != prefix ? (prefix + '-') : "") + event_name + (null != suffix ? ('-' + suffix) : "");
    }

    public static string to_consume_event(string event_name) {
        return to_event_string(event_name, "CONSUME");
    }

    public static string to_consumer_event(string event_name, string prefix, bool is_all = false) {
        if (is_all)
            return to_event_string(event_name, prefix.ToLower());
        return to_event_string(event_name, prefix).ToLower();
    }

    public static string to_ondisconnect_event(string id) {
        return to_event_string(id, "DISCONNECT");
    }

    public static string to_onunsubscribe_event(string event_name, string id) {
        // string eventStr = to_event_string(event_name);
        // return "UNSUBSCRIBE-" + eventStr + "-" + id;
        return to_event_string(event_name, "UNSUBSCRIBE", id);
    }

    public static string to_onsubscribe_event(string id) {
        // return "SUBSCRIBE-TO" + ("-" + id != null ? id : "");
        return to_event_string("TO", "SUBSCRIBE", id);
    }

    public static string to_consume_all_event(string producer) {
        return to_event_string("CONSUME") + to_consumer_all_event(producer);
    }

    public static string to_consumer_all_event (string producer) {
        // return 'producer + "-ALL";
        return to_event_string(producer.ToLower(), null, Constants.EVENT_ALL);
    }
}