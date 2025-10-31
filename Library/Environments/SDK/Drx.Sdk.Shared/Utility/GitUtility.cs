using System.Text.Json;
using System.IO;
using System.Text;

namespace Drx.Sdk.Shared.Utility
{
    public class GitHubContent
    {
        public string name { get; set; }
        public string path { get; set; }
        public string type { get; set; }
        public string download_url { get; set; }
        public string sha { get; set; }
    }

    /// <summary>
    /// Git 工具类
    /// </summary>
    public static class GitUtility
    {
        public static async Task<List<GitHubContent>> GetRepoAllFileName(string repoUrl, string branch = "main", string? personalAccessToken = null)
        {
            // 解析 repoUrl，假设格式为 https://github.com/owner/repo
            var uri = new Uri(repoUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2)
            {
                throw new ArgumentException("无效的仓库 URL");
            }
            var owner = segments[0];
            var repo = segments[1];

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);
            }

            return await GetAllFiles(owner, repo, "", branch, client);
        }

        private static async Task<List<GitHubContent>> GetAllFiles(string owner, string repo, string path, string branch, HttpClient client)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var contents = JsonSerializer.Deserialize<List<GitHubContent>>(json);

            var allContents = new List<GitHubContent>();
            foreach (var item in contents!)
            {
                if (item.type == "file")
                {
                    allContents.Add(item);
                }
                else if (item.type == "dir")
                {
                    var subContents = await GetAllFiles(owner, repo, item.path, branch, client);
                    allContents.AddRange(subContents);
                }
            }
            return allContents;
        }

        public static async Task<string> GetRawFileUrl(string repoUrl, string fileName, string branch = "main", string? personalAccessToken = null)
        {
            // 解析 repoUrl，假设格式为 https://github.com/owner/repo
            // 在该仓库内搜索所有文件，找到匹配 fileName 的文件，并返回其 Raw URL
            var contents = await GetRepoAllFileName(repoUrl, branch, personalAccessToken);
            var file = contents.FirstOrDefault(c => c.type == "file" && c.path == fileName);
            if (file != null)
            {
                return file.download_url;
            }
            throw new FileNotFoundException($"文件 '{fileName}' 在仓库 '{repoUrl}' 的分支 '{branch}' 中未找到");
        }

        public static async Task<bool> DownloadRaw(string repoUrl, string fileName, string savePath, string branch = "main", string? personalAccessToken = null)
        {
            // 使用 GetRawFileUrl 获取文件的 Raw URL，然后下载文件并保存到指定路径
            var rawUrl = await GetRawFileUrl(repoUrl, fileName, branch, personalAccessToken);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);
            }
            var response = await client.GetAsync(rawUrl);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(savePath, bytes);
                return true;
            }
            return false;
        }

        public static async Task<bool> PushFile(string repoUrl, string filePath, string repoPath, string commitMessage, string branch = "main", string? personalAccessToken = null)
        {
            // validate repoUrl
            if (string.IsNullOrWhiteSpace(repoUrl) || !Uri.TryCreate(repoUrl, UriKind.Absolute, out var _))
            {
                throw new ArgumentException("无效的仓库 URL", nameof(repoUrl));
            }

            // 解析 repoUrl，假设格式为 https://github.com/owner/repo
            var uri = new Uri(repoUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2)
            {
                throw new ArgumentException("无效的仓库 URL");
            }
            var owner = segments[0];
            var repo = segments[1];

            if (string.IsNullOrEmpty(personalAccessToken))
            {
                throw new ArgumentException("需要 personalAccessToken 来推送文件");
            }

            // 读取本地文件内容
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("本地文件不存在", filePath);
            }
            var fileContent = await File.ReadAllBytesAsync(filePath);
            var base64Content = Convert.ToBase64String(fileContent);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);

            // 检查文件是否存在，获取 SHA（如果存在）
            string? sha = null;
            var getUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{repoPath}?ref={branch}";
            var getResponse = await client.GetAsync(getUrl);
            if (getResponse.IsSuccessStatusCode)
            {
                var json = await getResponse.Content.ReadAsStringAsync();
                try
                {
                    var contentObj = JsonSerializer.Deserialize<GitHubContent>(json);
                    sha = contentObj?.sha;
                }
                catch
                {
                    // ignore parse errors - treat as not existing
                }
            }
            else
            {
                // If GET returned something other than 404, include details for diagnostics
                if (getResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var err = await getResponse.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"检查目标文件时失败: {getResponse.StatusCode} {getResponse.ReasonPhrase}: {err}");
                }
            }

            // 推送文件
            var putUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{repoPath}";
            var body = new
            {
                message = commitMessage,
                content = base64Content,
                branch = branch,
                sha = sha
            };
            var jsonBody = JsonSerializer.Serialize(body);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var putResponse = await client.PutAsync(putUrl, content);

            if (putResponse.IsSuccessStatusCode)
            {
                return true;
            }

            // read response content for detailed error
            var respText = await putResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"上传失败: {putResponse.StatusCode} {putResponse.ReasonPhrase}: {respText}");
        }

        public static async Task<string> ReadFile(string repoUrl, string fileName, string branch = "main", string? personalAccessToken = null)
        {
            // 阅读文件的内容并返回字符串，而不是下载到本地，filename可以为： "path/to/file.txt"
            var rawUrl = await GetRawFileUrl(repoUrl, fileName, branch, personalAccessToken);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);
            }
            var response = await client.GetAsync(rawUrl);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            throw new FileNotFoundException($"无法读取文件 '{fileName}'");
        }

        public static async Task<List<string>> GetRawFileUrls(string repoUrl, string fileNamePattern, string branch = "main", string? personalAccessToken = null)
        {
            // 支持通配符，如 "*.ini"，返回匹配文件的 Raw URLs 列表
            var contents = await GetRepoAllFileName(repoUrl, branch, personalAccessToken);
            var matchingFiles = GetMatchingFiles(contents, fileNamePattern);
            return matchingFiles.Select(f => f.download_url).ToList();
        }

        public static async Task<List<string>> ReadFiles(string repoUrl, string fileNamePattern, string branch = "main", string? personalAccessToken = null)
        {
            // 支持通配符，如 "*.ini"，返回匹配文件的内容列表
            var urls = await GetRawFileUrls(repoUrl, fileNamePattern, branch, personalAccessToken);
            var results = new List<string>();
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);
            }
            foreach (var url in urls)
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    results.Add(content);
                }
                else
                {
                    results.Add(null); // 或抛异常
                }
            }
            return results;
        }

        public static async Task<bool> DownloadRawBatch(string repoUrl, string fileNamePattern, string saveDirectory, string branch = "main", string? personalAccessToken = null)
        {
            // 支持通配符，如 "*.ini"，批量下载匹配文件到指定目录
            var urls = await GetRawFileUrls(repoUrl, fileNamePattern, branch, personalAccessToken);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DRX.Environment/1.0");
            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", personalAccessToken);
            }
            bool allSuccess = true;
            foreach (var url in urls)
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                    var savePath = Path.Combine(saveDirectory, fileName);
                    await File.WriteAllBytesAsync(savePath, bytes);
                }
                else
                {
                    Console.WriteLine($"下载失败: {url}, 状态码: {response.StatusCode}");
                    allSuccess = false;
                }
            }
            return allSuccess;
        }

        private static List<GitHubContent> GetMatchingFiles(List<GitHubContent> contents, string pattern)
        {
            if (pattern.StartsWith("*."))
            {
                var ext = pattern.Substring(1);
                return contents.Where(c => c.type == "file" && c.path.EndsWith(ext)).ToList();
            }
            else
            {
                // 如果不是通配符，按路径匹配单个文件
                var file = contents.FirstOrDefault(c => c.type == "file" && c.path == pattern);
                return file != null ? new List<GitHubContent> { file } : new List<GitHubContent>();
            }
        }
    }
}