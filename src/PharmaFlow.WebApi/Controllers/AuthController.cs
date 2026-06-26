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

    /// <summary>登录</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> Login(
        [FromBody] LoginRequest request, [FromQuery] bool rememberMe = false)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(ApiResponse<LoginResult>.Fail(result.Message ?? "登录失败"));

        var hours = rememberMe ? 720 : (int.Parse(_configuration["Jwt:ExpirationHours"] ?? "24"));
        result.Message = JwtTokenHelper.GenerateToken(
            result.UserId ?? Guid.Empty,
            result.DisplayName ?? "",
            result.Role ?? "",
            result.Permissions,
            _configuration,
            hours);

        return Ok(ApiResponse<LoginResult>.Ok(result, "登录成功"));
    }

    /// <summary>修改密码</summary>
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

    /// <summary>用户列表（管理员）</summary>
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsers()
    {
        var users = await _authService.GetUsersAsync();
        return Ok(ApiResponse<List<UserDto>>.Ok(users));
    }

    /// <summary>创建用户（管理员）</summary>
    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest req)
    {
        try
        {
            var user = await _authService.CreateUserAsync(req);
            return Ok(ApiResponse<UserDto>.Ok(user, "用户创建成功"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    /// <summary>启用/禁用用户</summary>
    [HttpPut("users/{id:guid}/toggle-active")]
    public async Task<ActionResult<ApiResponse>> ToggleUserActive(Guid id)
    {
        var ok = await _authService.ToggleUserActiveAsync(id);
        return ok ? Ok(ApiResponse.Ok("操作成功")) : NotFound(ApiResponse.Fail("用户不存在"));
    }

    /// <summary>角色列表</summary>
    [HttpGet("roles")]
    public IActionResult GetRoles(
        [FromServices] PharmaFlow.Infrastructure.Data.AppDbContext db)
    {
        var roleList = db.Roles.ToList();
        var roles = roleList.Select(r => new
        {
            r.Id,
            r.Name,
            r.Description,
            Permissions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.Permissions) ?? new List<string>()
        }).ToList();
        return Ok(new { success = true, data = roles });
    }

    /// <summary>编辑用户</summary>
    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        var ok = await _authService.UpdateUserAsync(id, req);
        return ok ? Ok(ApiResponse.Ok("更新成功")) : NotFound(ApiResponse.Fail("用户不存在"));
    }

    /// <summary>删除用户</summary>
    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteUser(Guid id)
    {
        var ok = await _authService.DeleteUserAsync(id);
        return ok ? Ok(ApiResponse.Ok("已删除")) : NotFound(ApiResponse.Fail("用户不存在"));
    }

    /// <summary>重置密码（管理员）</summary>
    [HttpPut("users/{id:guid}/reset-password")]
    public async Task<ActionResult<ApiResponse>> ResetPassword(Guid id, [FromQuery] string newPassword)
    {
        var ok = await _authService.ResetPasswordAsync(id, newPassword);
        return ok ? Ok(ApiResponse.Ok("密码已重置")) : NotFound(ApiResponse.Fail("用户不存在"));
    }
}

public record ChangePasswordRequest(string OldPassword, string NewPassword);
