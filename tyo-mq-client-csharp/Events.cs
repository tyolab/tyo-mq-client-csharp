namespace TYO_MQ_CLIENT;

public static class Events {

    // event is a keyword in C#
    public static string to_event_string(string event_name) {
        
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
        return event_name;
    }

    public static string to_consume_event(string event_name) {
        string eventStr = to_event_string(event_name);
        return "CONSUME-" + eventStr;
    }

    public static string to_ondisconnect_event(string id) {
        return "DISCONNECT-" + id;
    }

    public static string to_onunsubscribe_event(string event_name, string id) {
        string eventStr = to_event_string(event_name);
        return "UNSUBSCRIBE-" + eventStr + "-" + id;
    }

    public static string to_onsubscribe_event(string id) {
        return "SUBSCRIBE-TO" + ("-" + id != null ? id : "");
    }
}