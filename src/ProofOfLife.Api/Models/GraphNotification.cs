using System.Text.Json.Serialization;

namespace ProofOfLife.Api.Models;

public class GraphNotificationPayload
{
    [JsonPropertyName("value")]
    public List<GraphChangeNotification>? Value { get; set; }
}

public class GraphChangeNotification
{
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    [JsonPropertyName("clientState")]
    public string? ClientState { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("resourceData")]
    public GraphPresenceResourceData? ResourceData { get; set; }

    // Lifecycle notifications (subscriptionRemoved, missed, reauthorizationRequired)
    [JsonPropertyName("lifecycleEvent")]
    public string? LifecycleEvent { get; set; }
}

public class GraphPresenceResourceData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("activity")]
    public string? Activity { get; set; }
}
