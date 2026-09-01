using System.Security.Claims;

namespace Jellyfin.Plugin.RemoveSeries.Helpers;

internal static class CurrentUser
{
    public static Guid? GetId(ClaimsPrincipal principal)
    {
        string? value = principal.Claims
            .FirstOrDefault(claim => claim.Type.Equals("Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }
}

