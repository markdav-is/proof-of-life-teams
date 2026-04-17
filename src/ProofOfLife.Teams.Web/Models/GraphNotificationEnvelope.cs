using System.Text.Json.Serialization;

namespace ProofOfLife.Teams.Web.Models;

public sealed record GraphNotificationEnvelope
{
    [JsonPropertyName("value")]
    public GraphNotificationItem[] Value { get; init; } = [];
}

public sealed record GraphNotificationItem
{
    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; init; } = string.Empty;

    [JsonPropertyName("clientState")]
    public string? ClientState { get; init; }

    [JsonPropertyName("changeType")]
    public string ChangeType { get; init; } = string.Empty;

    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    [JsonPropertyName("resourceData")]
    public GraphPresenceResourceData? ResourceData { get; init; }
}

public sealed record GraphPresenceResourceData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("availability")]
    public string? Availability { get; init; }

    [JsonPropertyName("activity")]
    public string? Activity { get; init; }
}
