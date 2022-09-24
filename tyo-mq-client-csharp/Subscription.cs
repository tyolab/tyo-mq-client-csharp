namespace tyo_mq_client_csharp;

public enum SubscriptionType { FREE, PAID_MEMBER};

public class Subscription {
    public SubscriptionType type { get; set; }

    public string? id { get; set; }

    public string? alias { get; set; }

    public string? name { get; set; }

    public string? token { get; set; }

    public string? uri { get; set; }

    public Subscription(string id) {
        this.id = id;
    }
}