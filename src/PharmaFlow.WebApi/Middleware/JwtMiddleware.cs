using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PharmaFlow.WebApi.Middleware;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(
                    _configuration["Jwt:SecretKey"] ?? "PharmaFlow-Default-SecretKey-2024");

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var claims = jwtToken.Claims.ToList();

                var identity = new ClaimsIdentity(claims, "jwt");
                context.User = new ClaimsPrincipal(identity);
            }
            catch
            {
                // Token validation failed; leave User unauthenticated
            }
        }

        await _next(context);
    }
}

public static class JwtTokenHelper
{
    public static string GenerateToken(
        Guid userId,
        string username,
        string role,
        List<string> permissions,
        IConfiguration configuration,
        int? hoursOverride = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]
                ?? "PharmaFlow-Default-SecretKey-2024"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
        };
        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var hours = hoursOverride ?? int.Parse(configuration["Jwt:ExpirationHours"] ?? "24");
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "PharmaFlow",
            audience: configuration["Jwt:Audience"] ?? "PharmaFlow",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
