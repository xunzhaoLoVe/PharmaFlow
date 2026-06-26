using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    public AuthService(AppDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null)
            return new LoginResult { Success = false, Message = "用户名或密码错误" };

        // 检查锁定状态
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes + 1;
            return new LoginResult { Success = false, Message = $"账户已锁定，请 {remaining} 分钟后重试" };
        }

        // 验证密码（兼容旧版无 salt 的密码）
        var hash = string.IsNullOrEmpty(user.PasswordSalt)
            ? HashPasswordOld(request.Password)
            : HashPassword(request.Password, user.PasswordSalt);
        if (user.PasswordHash != hash)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                user.FailedLoginAttempts = 0;
                await _db.SaveChangesAsync();
                return new LoginResult { Success = false, Message = $"密码错误次数过多，账户已锁定 {LockoutMinutes} 分钟" };
            }
            await _db.SaveChangesAsync();
            var remaining = MaxFailedAttempts - user.FailedLoginAttempts;
            return new LoginResult { Success = false, Message = $"用户名或密码错误，还剩 {remaining} 次尝试机会" };
        }

        // 登录成功，重置计数
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
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
        if (user == null) return false;

        var oldHash = HashPassword(oldPassword, user.PasswordSalt);
        if (user.PasswordHash != oldHash) return false;

        var newSalt = GenerateSalt();
        user.PasswordHash = HashPassword(newPassword, newSalt);
        user.PasswordSalt = newSalt;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        return await _db.Users.Include(u => u.Role)
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Phone = u.Phone,
                Email = u.Email,
                IsActive = u.IsActive,
                Role = u.Role != null ? u.Role.Name : null,
                RoleId = u.RoleId,
                LastLoginTime = u.LastLoginTime,
            })
            .ToListAsync();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            throw new InvalidOperationException("用户名已存在");

        var salt = GenerateSalt();
        var user = new Domain.Entities.User
        {
            Username = req.Username,
            PasswordHash = HashPassword(req.Password, salt),
            PasswordSalt = salt,
            DisplayName = req.DisplayName,
            Phone = req.Phone,
            Email = req.Email,
            IsActive = true,
            RoleId = req.RoleId,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Phone = user.Phone,
            Email = user.Email,
            IsActive = user.IsActive,
        };
    }

    public async Task<bool> ToggleUserActiveAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        var salt = GenerateSalt();
        user.PasswordHash = HashPassword(newPassword, salt);
        user.PasswordSalt = salt;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName;
        if (!string.IsNullOrWhiteSpace(req.Phone)) user.Phone = req.Phone;
        if (!string.IsNullOrWhiteSpace(req.Email)) user.Email = req.Email;
        if (req.RoleId.HasValue) user.RoleId = req.RoleId;
        if (req.IsActive.HasValue) user.IsActive = req.IsActive.Value;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── 密码工具 ──
    public static string HashPassword(string password, string salt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string HashPasswordOld(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
