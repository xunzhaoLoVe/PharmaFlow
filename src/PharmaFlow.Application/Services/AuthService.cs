using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var hash = HashPassword(request.Password);
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.PasswordHash == hash && u.IsActive);

        if (user == null)
            return new LoginResult { Success = false, Message = "用户名或密码错误" };

        user.LastLoginTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var permissions = user.Role != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(user.Role.Permissions) ?? new List<string>()
            : new List<string>();

        return new LoginResult
        {
            Success = true,
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Role = user.Role?.Name,
            Permissions = permissions,
        };
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.PasswordHash != HashPassword(oldPassword))
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}
