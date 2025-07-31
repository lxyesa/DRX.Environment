using KaxServer.Models;
using KaxServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KaxServer.Controllers
{
    public class LoginRequest
    {
        public string UserNameOrEmail { get; set; }
        public string Password { get; set; }
    }

    [ApiController]
    [Route("v1/user")]
    public class UserController : ControllerBase
    {
        [HttpGet("/info/{userId}")]
        public async Task<IActionResult> GetUserInfoAsync(int userId)
        {
            var userInfo = await UserManager.GetUserByIdAsync(userId);

            var userInfoJson = new
            {
                userid = userInfo?.Id,
                username = userInfo?.Username,
                email = userInfo?.Email,
                coins = userInfo?.Coins,
                level = userInfo?.Level,
                exp = userInfo?.Exp,
                nextLevelExp = userInfo?.NextLevelExp,
                isAdmin = userInfo?.UserStatusData.IsAdmin,
                isBanned = userInfo?.UserStatusData.IsBanned,
                isAppLogin = userInfo?.UserStatusData.IsAppLogin,
                isWebLogin = userInfo?.UserStatusData.IsWebLogin,
                appToken = userInfo?.UserStatusData.AppToken
            };

            return Ok(userInfoJson);
        }
        
        [HttpPost("/logout-app")]
        public async Task<IActionResult> LogoutAppAsync([FromBody] int userId)
        {
            var result = await UserManager.LogoutAppAsync(userId);
            if (result)
            {
                return Ok(new { message = "注销成功" });
            }
            return BadRequest(new { message = "注销失败" });
        }

        [HttpPost("/edit-user")]
        public async Task<IActionResult> EditUserAsync([FromBody] UserData user)
        {
            if (user == null || user.Id <= 0)
            {
                return BadRequest(new { message = "用户数据无效" });
            }
            var result = await UserManager.SaveOrUpdateUserAsync(user);
            if (result)
            {
                return Ok(new { message = "保存成功" });
            }
            return BadRequest(new { message = "保存失败" });
        }
    }
}