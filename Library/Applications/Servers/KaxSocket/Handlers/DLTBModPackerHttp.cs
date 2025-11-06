using System.Dynamic;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using Drx.Sdk.Shared.Utility;
using Newtonsoft.Json.Linq;

namespace KaxSocket
{
    public class DLTBModPackerHttp
    {
        [HttpHandle("/api/dltbmodpacker/version/check", "GET")]
        public static HttpResponse GetCheckVersion(HttpRequest request)
        {
            var version = ConfigUtility.Read("configs.ini", "version", "general");
            if (string.IsNullOrEmpty(version))
            {
                Logger.Warn("configs.ini 中未找到版本号，已写入默认版本号1.0.0");
                version = "1.0.0";
                ConfigUtility.Push("configs.ini", "version", version, "general");
            }

            var body = new JsonObject
            {
                { "version", version }
            };

            var response = new HttpResponse
            {
                StatusCode = 200,
                Body = body.ToJsonString()
            };

            return response;
        }

        [HttpHandle("/api/dltbmodpacker/init/", "POST")]
        public static HttpResponse PostInit(HttpRequest request)
        {
            // 验证 User-Agent: DLTBModPacker/x.x.x
            var userAgent = request.Headers["User-Agent"];
            var clientVer = request.Headers["Client-Version"];
            var version = ConfigUtility.Read("configs.ini", "version", "general");

            if (string.IsNullOrEmpty(userAgent))
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少 User-Agent 头部"
                };
            }

            if (string.IsNullOrEmpty(clientVer))
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少 Client-Version 头部"
                };
            }

            //服务器版本缺省为1.0.0
            if (string.IsNullOrEmpty(version)) version = "1.0.0";

            if (clientVer != version)
            {
                return new HttpResponse
                {
                    StatusCode = 426,
                    Body = "客户端版本过旧，请更新至最新版本"
                };
            }


            // 初始化成功
            return new HttpResponse
            {
                StatusCode = 200,
                Body = "初始化成功"
            };
        }

        [HttpHandle("/api/dltbmodpacker/mod/getall", "GET")]
        public static HttpResponse GetAllMod(HttpRequest request)
        {
            // 获取所有Mod信息，然后组装为json数组
            var modInfoDb = Global.Instance.GetModInfoDataBase();
            if (modInfoDb != null)
            {
                var allMods = modInfoDb.GetAll();
                var modsArray = new JObject
                {
                    { "mods", JArray.FromObject(allMods) }
                };
                return new HttpResponse
                {
                    StatusCode = 200,
                    Body = modsArray.ToString()
                };
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode = 500,
                    Body = "Mod信息数据库未初始化"
                };
            }
        }

        [HttpHandle("/api/dltbmodpacker/mod/upload", "POST")]
        public static HttpResponse PostUploadMod(HttpRequest request)
        {
            // 请求的 Body 与 UploadFile由 HttpServer.ParseRequestAsync解析并填充（包括 multipart 流式解析）
            var reqBody = request.Body;

            if (string.IsNullOrEmpty(reqBody) && (request.UploadFile == null || request.UploadFile.Stream == null))
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少请求体或上传的文件"
                };
            }

            if (string.IsNullOrEmpty(reqBody))
            {
                // 没有 metadata，但可能有上传文件；返回错误以要求 metadata
                return new HttpResponse
                {
                    StatusCode =400,
                    Body = "缺少 metadata（JSON）"
                };
            }

            JObject jsonObj;
            try
            {
                jsonObj = JObject.Parse(reqBody);
            }
            catch (Exception ex)
            {
                Logger.Error($"解析上传请求的 metadata失败: {ex}");
                return new HttpResponse
                {
                    StatusCode =400,
                    Body = "无效的 metadata JSON"
                };
            }

            var modName = jsonObj["mod_name"]?.ToString();
            var modVersion = jsonObj["mod_version"]?.ToString();
            var modAuthor = jsonObj["mod_author"]?.ToString();
            var modDescription = jsonObj["mod_description"]?.ToString();
            var uploadToken = jsonObj["upload_token"]?.ToString();

            if (uploadToken != ConfigUtility.Read("configs.ini", "upload_token", "general"))
            {
                return new HttpResponse
                {
                    StatusCode =401,
                    Body = "无效的上传令牌"
                };
            }

            if (modName != null && modVersion != null && modAuthor != null && modDescription != null)
            {
                Logger.Info($"收到Mod上传请求：{modName} v{modVersion} by {modAuthor}");

                var modId = HashUtility.ComputeMD5Hash($"{modName}-{modVersion}-{modAuthor}");

                Logger.Info($"分配Mod ID：{modId}");

                var modInfo = new ModInfo
                {
                    ModId = modId,
                    ModName = modName,
                    ModVersion = modVersion,
                    ModAuthor = modAuthor,
                    ModDescription = modDescription,
                };

                try
                {
                    var result = HttpServer.SaveUploadFile(request, "mods", $"{modId}.dltbmodpak");

                    // 检测数据库内是否已存在该Mod信息，若不存在则保存，而是更新
                    var existingMod = Global.Instance.GetModInfoDataBase().Query("ModId", modId);
                    if (existingMod.Count > 0)
                    {
                        Logger.Info($"Mod {modId} 已存在，更新信息");
                        Global.Instance.GetModInfoDataBase().EditWhere("ModId", modId, modInfo);
                    }
                    else
                    {
                        Logger.Info($"保存新的Mod信息：{modId}");
                        Global.Instance.GetModInfoDataBase().Push(modInfo);
                    }

                    Logger.Info($"完成操作，mod数量统计：{Global.Instance.GetModInfoDataBase().QueryAll().Count}");

                    return result;
                }
                catch (Exception ex)
                {
                    Logger.Error($"保存Mod信息时发生错误：{ex.Message}");
                    return new HttpResponse
                    {
                        StatusCode =500,
                        Body = "保存Mod信息时发生错误"
                    };
                }
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode =400,
                    Body = "缺少必要的Mod信息字段"
                };
            }
        }

        [HttpHandle("/api/dltbmodpacker/mod/{modid}/download", "GET")]
        public static HttpResponse GetDownloadMod(HttpRequest request)
        {
            if (request.PathParameters.TryGetValue("modid", out var modid))
            {
                // 根据modid向服务器运行目录下的mods文件夹请求对应的Mod包文件（.dltbmodpak）
                Logger.Info($"收到Mod下载请求：{modid}");

                var modsDir = Path.Combine(AppContext.BaseDirectory, "mods");
                var filePath = Path.Combine(modsDir, $"{modid}.dltbmodpak");

                if (!File.Exists(filePath))
                {
                    Logger.Warn($"请求的 Mod 文件不存在: {filePath}");
                    return new HttpResponse
                    {
                        StatusCode = 404,
                        Body = "Not Found"
                    };
                }

                var result = HttpServer.CreateFileResponse(filePath, $"{modid}.dltbmodpak", bandwidthLimitKb: 5120);
                return result;
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少modid参数"
                };
            }
        }
        
        [HttpHandle("/api/dltbmodpacker/mod/{modid}/filesize", "GET")]
        public static HttpResponse GetModFileSize(HttpRequest request)
        {
            if (request.PathParameters.TryGetValue("modid", out var modid))
            {
                Logger.Info($"查询Mod {modid} 的文件大小");

                var modsDir = Path.Combine(AppContext.BaseDirectory, "mods");
                var filePath = Path.Combine(modsDir, $"{modid}.dltbmodpak");

                if (!File.Exists(filePath))
                {
                    Logger.Warn($"请求的 Mod 文件不存在: {filePath}");
                    return new HttpResponse
                    {
                        StatusCode = 404,
                        Body = "Not Found"
                    };
                }

                var fileInfo = new FileInfo(filePath);
                var result = new HttpResponse
                {
                    StatusCode = 200,
                    Body = fileInfo.Length.ToString()
                };
                return result;
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少modid参数"
                };
            }
        }

        [HttpHandle("/api/dltbmodpacker/mod/{modid}/version", "GET")]
        public static HttpResponse GetModVersion(HttpRequest request)
        {
            if (request.PathParameters.TryGetValue("modid", out var modid))
            {
                // 这里可以根据modid查询版本信息
                Logger.Info($"查询Mod {modid} 的版本");
                return new HttpResponse
                {
                    StatusCode = 200,
                    Body = $"Mod {modid} 的版本是 1.0.0"
                };
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少modid参数"
                };
            }
        }
    }
}
