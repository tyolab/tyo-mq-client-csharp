/**
  Define the possible Message Bodies her
  generally:
  {
    "id": xxxx,
    "event": EVENT_NAME,
  }
 */

 public class Message {
    public string id { get; set; }

    public string eventName { get; set; }

    public string body { get; set; }
 }