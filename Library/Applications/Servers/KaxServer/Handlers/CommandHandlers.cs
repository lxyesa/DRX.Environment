using System.Linq;
using Drx.Sdk.Network.Extensions;
using KaxServer.Services;
using System.Threading.Tasks;
using Drx.Sdk.Network.Socket;
using static Drx.Sdk.Network.Extensions.ConsoleCommandProcessor;

public static class CommandHandler
{
    public static void Registers(ConsoleCommandProcessor processor)
    {
        processor.RegisterCommand("test", (string[] args) =>
        {
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }
            Console.WriteLine("test");
            return true;
        });

        processor.RegisterCommand("stop", (string[] args) =>
        {
            Console.WriteLine("Stopping server...");
            Environment.Exit(0);
            return true;
        });

        processor.RegisterCommand("set", (string[] args) =>
        {
            if (args.Length == 0 || args[0] == "perm")
            {
                var result = SetPerm(args);
                return result.Result;
            }

            return false;
        });

        processor.RegisterCommand("csi", (string[] args) =>
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法：csi <标题> <描述> <用户ID>");
                return false;
            }

            var result = CreateStoreItem(args);
            return result.Result;
        });

        processor.RegisterCommand("create-store-item", (string[] args) =>
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法：create-store-item <标题> <描述> <用户ID>");
                return false;
            }

            var result = CreateStoreItem(args);
            return result.Result;
        });

        processor.RegisterCommand("store", (string[] args) =>
        {
            if (args[0] == "list")
            {
                var result = ListStoreItem(args);
                return result.Result;
            }

            if (args[0] == "clear")
            {
                var result = ClearStoreItem(args);
                return result.Result;
            }

            if (args[0] == "delete")
            {
                var result = DeleteStoreItem(args);
                return result.Result;
            }

            if (args[0] == "create")
            {
                var argsToPass = args.Skip(1).ToArray();
                var result = CreateStoreItem(argsToPass);
                return result.Result;
            }

            if (args[0] == "edit")
            {
                var argsToPass = args.Skip(1).ToArray();
                var result = EditStoreItem(argsToPass);
                return result.Result;
            }

            Console.Error.WriteLine("用法：store <T> <参数>，请使用 'help' 获取更多信息。");

            return false;
        });

        processor.RegisterCommand("user", (string[] args) =>
        {
            if (args[0] == "list")
            {
                var result = ListUser(args);
                return result.Result;
            }

            if (args[0] == "login-status")
            {
                var result = UserLoginStatus(args);
                return result.Result;
            }

            if (args[0] == "kick" && args.Length == 2)
            {
                var result = UserKick(args);
                return result.Result;
            }

            return false;
        });

        RegisterUsages(processor);
    }

    private static void RegisterUsages(ConsoleCommandProcessor processor)
    {
        processor.RegisterUsage("create-store-item", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：create-store-item <标题> <描述> <用户ID>");
            infoLine.Add("       csi <标题> <描述> <用户ID>");
            infoLine.Add("创建一个商品条目");
            infoLine.Add("<用户ID> 是用户编号，可通过 'user list' 获取");
            infoLine.Add("<标题> 是商品名称");
            infoLine.Add("<描述> 是商品描述");
            infoLine.Add("csi 是 create-store-item 的简写");
            return true;
        });

        processor.RegisterUsage("store", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：store <T>");
            infoLine.Add("当 <T> = list 时，列出所有商品条目");
            infoLine.Add("当 <T> = clear 时，清空所有商品条目");
            infoLine.Add("当 <T> = delete 时，命令用法：store delete <编号>");
            infoLine.Add("当 <T> = create 时，命令用法：store create <标题> <描述> <用户ID>");
            return true;
        });

        processor.RegisterUsage("user", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：user <T>");
            infoLine.Add("当 <T> = list 时，列出所有用户");
            infoLine.Add("当 <T> = kick <用户名> 时，强制断开指定用户连接");
            infoLine.Add("当 <T> = login-status 时，显示所有用户登录状态");
            infoLine.Add("当 <T> = login-status <用户名> 时，显示指定用户登录状态");
            return true;
        });

        processor.RegisterUsage("set", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：set perm <用户名> <权限>");
            infoLine.Add("设置用户权限，1 或 admin 为管理员，0 或 user 为普通用户");
            return true;
        });

        processor.RegisterUsage("test", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：test <参数1> <参数2> <参数3>");
            infoLine.Add("测试命令，无实际操作");
            return true;
        });

        processor.RegisterUsage("help", (CommandInfoLine infoLine) =>
        {
            infoLine.Add("用法：help");
            infoLine.Add("显示帮助信息");
            return true;
        });
    }

    private static async Task<bool> EditStoreItem(string[] args)
    {
        // TODO: 明天完成的一个商品编辑器，要求实现：
        // store edit price <storeItemId> clear
        // store edit price <storeItemId> add <title> <price> <rebate> <duration> <durationUnit>

        await Task.CompletedTask;
        return false;
    }

    private static async Task<bool> UserLoginStatus(string[] args)
    {
        if (args.Length < 2)
        {
            // 无用户名参数，返回所有用户状态
            var userList = await UserManager.GetAllUsersAsync();
            if (userList == null || userList.Count == 0)
            {
                Console.WriteLine("未找到用户");
                return true;
            }
            foreach (var user in userList)
            {
                var status = user.UserStatusData.IsAppLogin ? "Online" : "Offline";
                Console.WriteLine($"===================================================");
                Console.WriteLine($"用户 {user.Username} 状态：{status}");
                Console.WriteLine($"用户ID：{user.Id}");
                Console.WriteLine($"用户令牌(App)：{user.UserStatusData.AppToken}");
            }
            return true;
        }

        var username = args[1];
        var singleUser = await UserManager.GetUserByUsernameAsync(username);
        if (singleUser == null)
        {
            Console.Error.WriteLine($"未找到用户：{username}");
            return false;
        }

        // 移除设置 login-status 的功能，仅允许查询

        var singleStatus = singleUser.UserStatusData.IsAppLogin ? "在线" : "离线";
        Console.WriteLine($"用户 {username} 状态：{singleStatus}");
        Console.WriteLine($"用户ID：{singleUser.Id}");
        Console.WriteLine($"用户令牌(App)：{singleUser.UserStatusData.AppToken}");
        return true;
    }

    private static async Task<bool> DeleteStoreItem(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法：store delete <编号>");
            return false;
        }

        var result = await StoreManager.DeleteStoreItemAsync(int.Parse(args[1]));
        if (result)
        {
            Console.WriteLine("商品条目删除成功");
        }
        else
        {
            Console.Error.WriteLine("商品条目删除失败");
        }

        return result;
    }

    private static async Task<bool> ClearStoreItem(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("用法：store clear，无需任何参数");
            return false;
        }

        await StoreManager.ClearStoreItemsAsync();
        Console.WriteLine("商品条目清空成功");
        return true;
    }

    private static async Task<bool> ListUser(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("用法：user list");
            return false;
        }

        var userList = await UserManager.GetAllUsersAsync();
        foreach (var user in userList)
        {
            Console.WriteLine($"编号：{user.Id}，用户名：{user.Username}，管理员：{user.UserStatusData.IsAdmin}");
        }
        return true;
    }

    private static async Task<bool> ListStoreItem(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("用法：store list，无需任何参数");
            return false;
        }

        var storeItemList = await StoreManager.GetAllStoreItemsAsync();
        if (storeItemList.Count == 0)
        {
            Console.WriteLine("未找到商品条目");
            return true;
        }
        foreach (var item in storeItemList)
        {
            Console.WriteLine($"编号：{item.Id}，名称：{item.Title}，用户ID：{item.OwnerId}");
        }
        return true;
    }

    private static async Task<bool> CreateStoreItem(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("用法：store create <标题> <描述> <用户ID>");
            return false;
        }

        var itemId = StoreManager.GetAllStoreItemsAsync().Result.Count + 1;

        var result = StoreManager.CreateStoreItemAsync(args[0], args[1], int.Parse(args[2]), itemId);
        if (result.Result)
        {
            var user = UserManager.GetUserByIdAsync(int.Parse(args[2]));
            if (user != null)
            {
                if (user.Result != null)
                {
                    user.Result.PublishedStoreItemIds.Add(itemId);
                    await UserManager.SaveOrUpdateUserAsync(user.Result);
                    Console.WriteLine($"商品条目创建成功，编号：{itemId}，拥有者：{user.Result.Username}");
                }
                else
                {
                    Console.WriteLine($"[WARN] 商品条目创建成功，但未找到用户，编号：{itemId}，用户ID：{args[2]}");
                }
            }
        }
        return result.Result;
    }

    private static async Task<bool> UserKick(string[] args)
    {
        var username = args[1];
        var user = await UserManager.GetUserByUsernameAsync(username);
        if (user == null)
        {
            Console.Error.WriteLine($"未找到用户：{username}");
            return false;
        }

        // 踢出用户：断开 App 登录状态并清空 Token
        user.UserStatusData.IsAppLogin = false;
        user.UserStatusData.AppToken = string.Empty;
        await UserManager.SaveOrUpdateUserAsync(user);

        Console.WriteLine($"用户 {username} 已被踢出。");
        return true;
    }

    private static async Task<bool> SetPerm(string[] args)
    {
        if (args.Length == 0 || args[0] != "perm")
        {
            Console.Error.WriteLine("用法：set perm <用户名> <权限>");
            return false;
        }

        if (args.Length < 3)
        {
            Console.Error.WriteLine("用法：set perm <用户名> <权限>");
            return false;
        }

        if (string.IsNullOrEmpty(args[1]))
        {
            Console.Error.WriteLine("缺少参数：set perm --> <用户名> <-- <权限>");
            return false;
        }

        var userList = await UserManager.GetAllUsersAsync();
        var user = userList.FirstOrDefault(u => u.Username == args[1]);
        if (user == null)
        {
            Console.Error.WriteLine($"未找到用户：{args[1]}");
            return false;
        }

        if (string.IsNullOrEmpty(args[2]))
        {
            Console.Error.WriteLine("缺少参数：set perm <用户名> <权限>");
            return false;
        }

        if (args[2] == "1" || args[2].ToLower() == "admin")
        {
            user.UserStatusData.IsAdmin = true;
            await UserManager.SaveOrUpdateUserAsync(user);
            Console.WriteLine($"已将用户 {user.Username} 设置为管理员");
            return true;
        }
        if (args[2] == "0" || args[2].ToLower() == "user")
        {
            user.UserStatusData.IsAdmin = false;
            await UserManager.SaveOrUpdateUserAsync(user);
            Console.WriteLine($"已将用户 {user.Username} 设置为普通用户");
            return true;
        }

        return false;
    }
}