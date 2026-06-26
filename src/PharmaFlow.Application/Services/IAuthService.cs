using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword);
    Task<List<UserDto>> GetUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserRequest req);
    Task<bool> ToggleUserActiveAsync(Guid userId);
    Task<bool> ResetPasswordAsync(Guid userId, string newPassword);
    Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest req);
    Task<bool> DeleteUserAsync(Guid userId);
}
