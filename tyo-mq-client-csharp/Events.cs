namespace tyo_mq_client_csharp;

    public class Events {

        // event is a keyword in C#
        public string to_event_string(string event_name) {
            
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

        public string to_consume_event(Events cls, string event_name) {
            string eventStr = cls.to_event_string(event_name);
            return "CONSUME-" + eventStr;

        }

        public string to_ondisconnect_event(string id) {
            return "DISCONNECT-" + id;

        }

        public string to_onunsubscribe_event(Events cls, string event_name, string id) {
            string eventStr = cls.to_event_string(event_name);
            return "UNSUBSCRIBE-" + eventStr + "-" + id;

        }

        public string to_onsubscribe_event(string id) {
            return "SUBSCRIBE-TO" + ("-" + id != null ? id : "");
        }
    }