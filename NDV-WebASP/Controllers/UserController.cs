using Microsoft.AspNetCore.Mvc;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using NDV.WebASP.Services;
using NetworkCoreStandard.Models;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly JwtService _jwtService;

    public UserController(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    // DTO Models
    public class LoginRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string MachineCode { get; set; }
    }

    public class RegisterRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Email { get; set; }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var userManager = Program.Server.GetUserManager();
        var clientIP = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;

        // 首先检查用户是否已有有效token
        var existingUser = userManager.GetUserByUsername(request.Username);
        if (existingUser?.CurrentToken != null)
        {
            var (success, user) = await userManager.ReactivateUserAsync(request.Username, existingUser.CurrentToken);
            if (success && user != null)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = "登录成功",
                    token = user.CurrentToken,
                    user = new {
                        username = user.Username,
                        email = user.Email,
                        userGroup = user.UserGroup,
                        lastLoginTime = user.LastLoginTime
                    }
                });
            }
        }

        // 如果没有有效token，执行正常登录流程
        var (result, newUser) = await userManager.LoginUserAsync(
            request.Username,
            request.Password,
            request.MachineCode,
            clientIP
        );

        if (result == UserLoginResult.Success && newUser != null)
        {
            var token = _jwtService.GenerateToken(newUser);
            newUser.CurrentToken = token;  // 保存token到用户实例

            return Ok(new 
            { 
                success = true, 
                message = "登录成功",
                token = token,
                user = new {
                    username = newUser.Username,
                    email = newUser.Email,
                    userGroup = newUser.UserGroup,
                    lastLoginTime = newUser.LastLoginTime
                }
            });
        }

        return result switch
        {
            UserLoginResult.UserNotFound => NotFound(new { success = false, message = "用户不存在" }),
            UserLoginResult.WrongPassword => BadRequest(new { success = false, message = "密码错误" }),
            UserLoginResult.AlreadyOnline => Conflict(new { success = false, message = "用户已在线" }),
            _ => BadRequest(new { success = false, message = "未知错误" })
        };
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var userManager = Program.Server.GetUserManager();
        var (success, message) = await userManager.RegisterUserAsync(
            request.Username,
            request.Password,
            request.Email
        );

        if (success)
        {
            return Ok(new { success = true, message });
        }
        
        return BadRequest(new { success = false, message });
    }

    [Authorize]
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var userManager = Program.Server.GetUserManager();
        var user = userManager.GetUserByUsername(username);
        
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            username = user.Username,
            email = user.Email,
            userGroup = user.UserGroup,
            lastLoginTime = user.LastLoginTime
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(username))
        {
            Program.Server.GetUserManager().LogoutUser(username);
            return Ok(new { success = true, message = "已成功登出" });
        }
        return BadRequest(new { success = false, message = "登出失败" });
    }
}