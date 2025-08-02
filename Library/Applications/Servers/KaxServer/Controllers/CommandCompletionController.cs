using Microsoft.AspNetCore.Mvc;
using Drx.Sdk.Shared.ConsoleCommand;
using System.Text.RegularExpressions;

namespace KaxServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandCompletionController : ControllerBase
    {
        /// <summary>
        /// 获取命令补全建议
        /// </summary>
        [HttpGet("suggestions")]
        public IActionResult GetCommandSuggestions([FromQuery] string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Ok(new { suggestions = new List<CommandSuggestion>() });
            }

            var suggestions = new List<CommandSuggestion>();
            var allCommands = ConsoleCommandProcessor.GetAllCommands();
            
            input = input.Trim();
            
            // 处理空输入或只有/的情况
            if (input == "/" || string.IsNullOrWhiteSpace(input.TrimStart('/')))
            {
                foreach (var cmd in allCommands)
                {
                    suggestions.Add(new CommandSuggestion
                    {
                        Text = $"/{cmd.Name}",
                        Description = cmd.Description,
                        Type = "command",
                        Level = 0
                    });
                }
                return Ok(new { suggestions });
            }

            // 解析当前输入
            var parts = ParseCommandInput(input);
            if (parts == null || parts.Length == 0)
            {
                return Ok(new { suggestions });
            }

            // 获取主命令
            var mainCommand = parts[0].TrimStart('/');
            var mainCmdInfo = allCommands.FirstOrDefault(c => c.Name.Equals(mainCommand, StringComparison.OrdinalIgnoreCase));
            
            if (mainCmdInfo == null)
            {
                // 主命令不存在，提供相似命令建议
                var similarCommands = allCommands
                    .Where(c => c.Name.StartsWith(mainCommand, StringComparison.OrdinalIgnoreCase))
                    .Take(5);
                
                foreach (var cmd in similarCommands)
                {
                    suggestions.Add(new CommandSuggestion
                    {
                        Text = $"/{cmd.Name}",
                        Description = cmd.Description,
                        Type = "command",
                        Level = 0
                    });
                }
                return Ok(new { suggestions });
            }

            // 分析当前层级
            var currentLevel = AnalyzeCurrentLevel(input, parts, mainCmdInfo);
            
            // 根据当前层级提供建议
            if (currentLevel.Level == 0)
            {
                // 主命令后，提供子命令建议
                var level1SubCommands = mainCmdInfo.SubCommands.Where(s => s.Level == 1).ToList();
                
                // 如果有部分文本，进行过滤
                if (!string.IsNullOrEmpty(currentLevel.PartialText))
                {
                    level1SubCommands = level1SubCommands
                        .Where(s => s.Name.StartsWith(currentLevel.PartialText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                foreach (var subCmd in level1SubCommands)
                {
                    string baseText;
                    if (input.EndsWith(" "))
                    {
                        baseText = input.TrimEnd();
                    }
                    else
                    {
                        // 移除部分输入
                        var lastSpaceIndex = input.LastIndexOf(' ');
                        if (lastSpaceIndex > 0)
                        {
                            baseText = input.Substring(0, lastSpaceIndex);
                        }
                        else
                        {
                            baseText = $"/{mainCommand}";
                        }
                    }

                    suggestions.Add(new CommandSuggestion
                    {
                        Text = $"{baseText} -{subCmd.Name}",
                        Description = subCmd.Description,
                        Type = "subcommand",
                        Level = 1,
                        Parent = mainCommand
                    });
                }
            }
            else if (currentLevel.Level >= 1)
            {
                // 检查是否有分支命令
                var hasBranchCommands = mainCmdInfo.BranchCommands.Any(b =>
                    b.ParentName.Equals(currentLevel.ParentCommand, StringComparison.OrdinalIgnoreCase));

                // 首先显示分支命令（如果有）
                if (hasBranchCommands)
                {
                    // 显示所有可用的分支命令
                    var availableBranchCommands = mainCmdInfo.BranchCommands
                        .Where(b => b.ParentName.Equals(currentLevel.ParentCommand, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // 如果有部分文本，进行过滤
                    if (!string.IsNullOrEmpty(currentLevel.PartialText))
                    {
                        availableBranchCommands = availableBranchCommands
                            .Where(b => b.Name.StartsWith(currentLevel.PartialText, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }

                    foreach (var branchCmd in availableBranchCommands)
                    {
                        string baseText;
                        if (input.EndsWith(" "))
                        {
                            baseText = input.TrimEnd();
                        }
                        else
                        {
                            var lastDashIndex = input.LastIndexOf('-');
                            if (lastDashIndex > 0)
                            {
                                var prefixStart = input.LastIndexOf(' ', lastDashIndex);
                                if (prefixStart > 0)
                                {
                                    baseText = input.Substring(0, prefixStart);
                                }
                                else
                                {
                                    baseText = $"/{mainCommand}";
                                }
                            }
                            else
                            {
                                baseText = $"/{mainCommand}";
                            }
                        }

                        suggestions.Add(new CommandSuggestion
                        {
                            Text = $"{baseText} -{branchCmd.Name}",
                            Description = branchCmd.Description,
                            Type = "branchcommand",
                            Level = currentLevel.Level + 1,
                            Parent = currentLevel.ParentCommand
                        });
                    }
                }

                // 修改：显示所有可访问的子命令，而不仅仅是下一级的
                // 这允许用户看到所有可用的子命令，无论层级如何
                var accessibleSubCommands = new List<ConsoleCommandProcessor.SubCommandInfo>();
                
                // 获取所有层级大于当前层级的子命令
                var allSubCommands = mainCmdInfo.SubCommands.ToList();
                
                // 根据当前上下文过滤可访问的子命令
                foreach (var subCmd in allSubCommands)
                {
                    // 如果当前层级为1，显示所有层级>=1的子命令
                    // 这样可以确保用户能看到所有可用的子命令
                    if (subCmd.Level > currentLevel.Level ||
                        (currentLevel.Level == 1 && subCmd.Level >= 1))
                    {
                        accessibleSubCommands.Add(subCmd);
                    }
                }

                // 如果有部分文本，进行过滤
                if (!string.IsNullOrEmpty(currentLevel.PartialText))
                {
                    accessibleSubCommands = accessibleSubCommands
                        .Where(s => s.Name.StartsWith(currentLevel.PartialText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // 添加匹配的子命令
                foreach (var subCmd in accessibleSubCommands)
                {
                    string baseText;
                    if (input.EndsWith(" "))
                    {
                        baseText = input.TrimEnd();
                    }
                    else
                    {
                        var lastDashIndex = input.LastIndexOf('-');
                        if (lastDashIndex > 0)
                        {
                            var prefixStart = input.LastIndexOf(' ', lastDashIndex);
                            if (prefixStart > 0)
                            {
                                baseText = input.Substring(0, prefixStart);
                            }
                            else
                            {
                                baseText = $"/{mainCommand}";
                            }
                        }
                        else
                        {
                            baseText = $"/{mainCommand}";
                        }
                    }

                    suggestions.Add(new CommandSuggestion
                    {
                        Text = $"{baseText} -{subCmd.Name}",
                        Description = subCmd.Description,
                        Type = "subcommand",
                        Level = subCmd.Level,
                        Parent = mainCommand
                    });
                }
            }

            // 限制建议数量
            suggestions = suggestions.Take(10).ToList();
            
            return Ok(new { suggestions });
        }

        /// <summary>
        /// 解析命令输入
        /// </summary>
        private string[] ParseCommandInput(string input)
        {
            // 移除前导空格
            input = input.Trim();
            
            // 分割命令部分
            var parts = Regex.Split(input, @"(?=-\w+)")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToArray();
            
            return parts;
        }

        /// <summary>
        /// 分析当前层级信息
        /// </summary>
        private LevelInfo AnalyzeCurrentLevel(string input, string[] parts, ConsoleCommandProcessor.CommandInfo mainCmdInfo)
        {
            var level = 0;
            var parentCommand = "";
            var partialText = "";
            
            if (parts.Length == 1)
            {
                // 只有主命令，层级为0
                return new LevelInfo { Level = 0 };
            }
            
            // 分析子命令层级 - 使用更准确的解析方式
            var commandParts = new List<string>();
            var allParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 1; i < allParts.Length; i++)
            {
                var part = allParts[i];
                if (part.StartsWith("-"))
                {
                    var cmdName = part.TrimStart('-');
                    commandParts.Add(cmdName);
                }
            }

            // 确定当前层级
            level = commandParts.Count;
            
            // 确定父命令 - 更准确的逻辑
            if (commandParts.Count > 0)
            {
                // 检查每个命令是否是分支命令的父命令
                for (int i = commandParts.Count - 1; i >= 0; i--)
                {
                    var cmdName = commandParts[i];
                    var isBranchParent = mainCmdInfo.BranchCommands.Any(b =>
                        b.ParentName.Equals(cmdName, StringComparison.OrdinalIgnoreCase));
                    
                    if (isBranchParent)
                    {
                        parentCommand = cmdName;
                        break;
                    }
                }
                
                // 如果没有找到分支命令父命令，使用最后一个子命令
                if (string.IsNullOrEmpty(parentCommand) && commandParts.Count > 0)
                {
                    parentCommand = commandParts.Last();
                }
            }

            // 检查部分文本 - 更准确的逻辑
            var lastPart = allParts.LastOrDefault();
            if (lastPart != null && lastPart.StartsWith("-") && !input.EndsWith(" "))
            {
                partialText = lastPart.TrimStart('-');
                // 只有在正在输入时才减1
                if (!string.IsNullOrEmpty(partialText))
                {
                    level = Math.Max(0, level - 1);
                }
            }

            return new LevelInfo
            {
                Level = level,
                ParentCommand = parentCommand,
                PartialText = partialText
            };
        }

        public class CommandSuggestion
        {
            public string Text { get; set; }
            public string Description { get; set; }
            public string Type { get; set; }
            public int Level { get; set; }
            public string Parent { get; set; }
        }

        private class LevelInfo
        {
            public int Level { get; set; }
            public string ParentCommand { get; set; }
            public string PartialText { get; set; }
        }
    }
}