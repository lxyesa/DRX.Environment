using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drx.Sdk.Network.Extensions;
using Drx.Sdk.Shared.ConsoleCommand;
using Drx.Sdk.Network.Socket;

namespace KaxServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly string _logsDirectory;

        public LogController()
        {
            _logsDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logs");
        }

        // 文件大小限制：50MB
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024;

        [HttpGet("latest")]
        public IActionResult GetLatestLogFile()
        {
            if (!Directory.Exists(_logsDirectory))
            {
                return NotFound(new { message = "日志目录不存在" });
            }

            var logFiles = Directory.GetFiles(_logsDirectory, "*.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.CreationTime)
                .ToList();

            if (logFiles.Count == 0)
            {
                return NotFound(new { message = "没有找到日志文件" });
            }

            return Ok(new { fileName = logFiles.First().Name });
        }

        [HttpGet("lines")]
        public async Task<IActionResult> GetLogLines(string fileName, int startLine, int count)
        {
            // 路径验证，防止路径穿越攻击
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest(new { message = "无效的文件名" });
            }

            // 确保文件名以.txt结尾
            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "只允许访问.txt文件" });
            }

            var filePath = Path.Combine(_logsDirectory, fileName);
            
            // 验证文件是否在日志目录内（双重检查）
            var fullPath = Path.GetFullPath(filePath);
            var logDirPath = Path.GetFullPath(_logsDirectory);
            if (!fullPath.StartsWith(logDirPath))
            {
                return BadRequest(new { message = "文件访问被拒绝" });
            }
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "日志文件不存在" });
            }

            // 检查文件大小
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MAX_FILE_SIZE)
            {
                return BadRequest(new { message = "日志文件过大，无法读取" });
            }

            try
            {
                // 使用Cancellation Token实现超时处理（10秒超时）
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var result = await ReadLinesWithPaginationAsync(filePath, startLine, count, cts.Token);
                    
                    return Ok(new {
                        lines = result.lines,
                        totalLines = result.totalLines,
                        startLine = result.startLine,
                        endLine = result.endLine
                    });
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { message = "读取日志文件超时" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = $"读取日志文件时出错: {ex.Message}" });
            }
        }

        private async Task<(List<string> lines, int totalLines, int startLine, int endLine)> ReadLinesWithPaginationAsync(
            string filePath, int startLine, int count, CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            
            // 第一次遍历：计算总行数
            int totalLines = 0;
            using (var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                while (await reader.ReadLineAsync() != null)
                {
                    totalLines++;
                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            
            // 处理负数起始行（从末尾开始计算）
            if (startLine < 0)
            {
                startLine = totalLines + startLine;
                if (startLine < 0) startLine = 0;
            }
            
            // 计算结束行
            var endLine = startLine + count;
            if (endLine > totalLines) endLine = totalLines;
            
            // 如果请求的范围超出文件范围，返回空结果
            if (startLine >= totalLines || count <= 0)
            {
                return (new List<string>(), totalLines, startLine, endLine);
            }
            
            // 第二次遍历：读取指定范围的行
            int currentLine = 0;
            using (var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 如果到达请求的范围，开始收集行
                    if (currentLine >= startLine && currentLine < endLine)
                    {
                        lines.Add(line);
                    }
                    // 如果已经收集完所有请求的行，提前退出
                    else if (currentLine >= endLine)
                    {
                        break;
                    }
                    
                    currentLine++;
                }
            }
            
            return (lines, totalLines, startLine, endLine);
        }

        /// <summary>
        /// 执行控制台命令
        /// </summary>
        [HttpPost("execute")]
        public IActionResult ExecuteCommand([FromBody] CommandRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Command))
            {
                return BadRequest(new { message = "命令不能为空" });
            }

            try
            {
                // 执行命令
                var result = ConsoleCommandProcessor.ExecuteCommand(request.Command);

                return Ok(new
                {
                    success = true,
                    result = result?.ToString() ?? "命令执行完成",
                    command = request.Command
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    command = request.Command
                });
            }
        }
    }

    public class CommandRequest
    {
        public string Command { get; set; }
    }
}