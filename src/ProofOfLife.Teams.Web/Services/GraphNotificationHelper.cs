namespace ProofOfLife.Teams.Web.Services;

public static class GraphNotificationHelper
{
    /// <summary>
    /// Extracts a UPN from a Microsoft Graph presence resource path.
    /// Graph presence subscriptions use the path format "users/{identifier}/presence".
    /// When the subscription is created using a UPN (e.g. alice@agency.example) rather
    /// than an object ID, the resource URL in notifications will contain the UPN and can
    /// be used directly to record presence without an additional Graph lookup.
    /// </summary>
    /// <param name="resource">
    /// The resource path from a Graph change notification, e.g. "users/alice@agency.example/presence".
    /// </param>
    /// <returns>
    /// The UPN string if the path matches the expected format and the identifier contains '@',
    /// otherwise <see langword="null"/>.
    /// </returns>
    public static string? ParseUpnFromResource(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        // Expected format: users/{identifier}/presence
        var parts = resource.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            string.Equals(parts[0], "users", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[^1], "presence", StringComparison.OrdinalIgnoreCase))
        {
            var identifier = parts[1];

            // Only accept identifiers that look like a UPN (object IDs do not contain '@')
            if (identifier.Contains('@'))
            {
                return identifier;
            }
        }

        return null;
    }
}
