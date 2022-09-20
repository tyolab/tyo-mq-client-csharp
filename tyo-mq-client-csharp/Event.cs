namespace tyo_mq_client_csharp;

using System.Text.Json;

public class Event {
    public string eventName { get; set; }
    public string message { get; set; }
}