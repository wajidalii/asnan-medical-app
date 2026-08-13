using System.Security.Claims;

namespace Asnan.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id claim present on the authenticated principal.");

        return Guid.Parse(value);
    }
}
