using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Middleware;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(ApiResponse<LoginResult>.Fail(result.Message ?? "登录失败"));

        result.Message = JwtTokenHelper.GenerateToken(
            result.UserId ?? Guid.Empty,
            result.DisplayName ?? "",
            result.Role ?? "",
            result.Permissions,
            _configuration);

        return Ok(ApiResponse<LoginResult>.Ok(result, "登录成功"));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(ApiResponse.Fail("未登录"));

        var ok = await _authService.ChangePasswordAsync(
            Guid.Parse(userIdClaim), request.OldPassword, request.NewPassword);

        return ok
            ? Ok(ApiResponse.Ok("密码修改成功"))
            : BadRequest(ApiResponse.Fail("原密码错误"));
    }
}

public record ChangePasswordRequest(string OldPassword, string NewPassword);
