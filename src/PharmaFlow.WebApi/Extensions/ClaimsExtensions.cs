using System.Security.Claims;

namespace PharmaFlow.WebApi.Extensions;

public static class ClaimsExtensions
{
    /// <summary>从 ClaimsPrincipal 提取用户 Id</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sid = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sid, out var id) ? id : Guid.Empty;
    }

    /// <summary>从 ClaimsPrincipal 提取用户名</summary>
    public static string? GetUserName(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value;
    }
}
