using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword);
}
