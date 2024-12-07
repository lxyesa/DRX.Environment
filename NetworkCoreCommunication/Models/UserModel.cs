using System;
using System.Net.Sockets;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Models;

public class UserModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public async Task Save()
    {
        try
        {
            string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");
            await NetworkCoreStandard.IO.File.WriteJsonKeyAsync(
                usersFilePath,
                Username,  // 使用用户名作为键
                this      // 将整个用户对象作为值
            );

            Logger.Log("User", $"用户 {Username} 的数据已保存");
        }
        catch (Exception ex)
        {
            Logger.Log("User", $"保存用户 {Username} 数据时发生错误: {ex.Message}");
            throw;
        }
    }
}
