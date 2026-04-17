using ProofOfLife.Teams.Web.Services;

namespace ProofOfLife.Teams.Web.Tests;

public class GraphNotificationHelperTests
{
    [Theory]
    [InlineData("users/alice@agency.example/presence", "alice@agency.example")]
    [InlineData("Users/alice@agency.example/Presence", "alice@agency.example")]
    [InlineData("users/ALICE@AGENCY.EXAMPLE/presence", "ALICE@AGENCY.EXAMPLE")]
    [InlineData("users/first.last@subdomain.agency.example/presence", "first.last@subdomain.agency.example")]
    public void ParseUpnFromResource_ValidUpnPaths_ReturnsUpn(string resource, string expectedUpn)
    {
        var result = GraphNotificationHelper.ParseUpnFromResource(resource);

        Assert.Equal(expectedUpn, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("communications/presences/aad-object-id")]
    [InlineData("users/aad-object-id-without-at/presence")]
    [InlineData("users/alice@agency.example")]
    [InlineData("presence")]
    public void ParseUpnFromResource_InvalidOrNonUpnPaths_ReturnsNull(string? resource)
    {
        var result = GraphNotificationHelper.ParseUpnFromResource(resource);

        Assert.Null(result);
    }
}
