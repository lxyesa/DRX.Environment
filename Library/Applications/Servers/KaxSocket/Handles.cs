using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using Drx.Sdk.Shared.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaxSocket
{
    public class Handles
    {
        [HttpHandle("/api/test", "GET")]
        public static HttpResponse GetTest(HttpRequest request)
        {
            var version = ConfigUtility.Read("KaxSocket.ini", "version", "general");
            if (string.IsNullOrEmpty(version))
            {
                Logger.Warn("KaxSocket.ini 中未找到版本号，已写入默认版本号1.0.0");
                version = "1.0.0";
                ConfigUtility.Push("KaxSocket.ini", "version", version, "general");
            }

            var dataSer = new DrxSerializationData
             {
                {"ver", version }
             };
            var response = new HttpResponse
            {
                StatusCode = 200,
                BodyBytes = dataSer.Serialize()
            };

            return response;
        }

        [HttpHandle("/api/init/", "POST")]
        public static HttpResponse PostInit(HttpRequest request)
        {
            // 验证 User-Agent: DLTBModPacker/x.x.x
            var userAgent = request.Headers["User-Agent"];
            var clientVer = request.Headers["Client-Version"];
            var version = ConfigUtility.Read("KaxSocket.ini", "version", "general");

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

        [HttpHandle("/api/user/register", "POST")]
        public static HttpResponse PostRegister(HttpRequest request)
        {
            // todo
            return null;
        }

        [HttpHandle("/api/user/login", "POST")]
        public static HttpResponse PostLogin(HttpRequest request)
        {
            // todo
            return null;
        }

        [HttpHandle("/api/mod/getall", "GET")]
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

        [HttpHandle("/api/mod/upload", "POST")]
        public static HttpResponse PostUploadMod(HttpRequest request)
        {
            // 首先获取请求中的拓展请求体
            var extraData = request.Body;
            if (extraData == null || extraData.Length == 0)
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少请求体"
                };
            }

            var jsonObj = JObject.Parse(extraData);
            var modName = jsonObj["mod_name"]?.ToString();
            var modVersion = jsonObj["mod_version"]?.ToString();
            var modAuthor = jsonObj["mod_author"]?.ToString();
            var modDescription = jsonObj["mod_description"]?.ToString();


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
                    Global.Instance.GetModInfoDataBase().Push(modInfo);
                }
                catch (Exception ex)
                {
                    Logger.Error($"保存Mod信息时发生错误：{ex.Message}");
                    return new HttpResponse
                    {
                        StatusCode = 500,
                        Body = "保存Mod信息时发生错误"
                    };
                }

                return new HttpResponse
                {
                    StatusCode = 200,
                    Body = "Mod上传成功"
                };
            }
            else
            {
                return new HttpResponse
                {
                    StatusCode = 400,
                    Body = "缺少必要的Mod信息字段"
                };
            }
        }

        [HttpHandle("/api/mod/download/{modid}", "GET")]
        public static HttpResponse GetDownloadMod(HttpRequest request)
        {
            if (request.PathParameters.TryGetValue("modid", out var modid))
            {
                // todo
                return null;
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

        [HttpHandle("/api/mod/{modid}/version", "GET")]
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

        [HttpHandle("/api/mod/testu", "POST", StreamUpload = true)]
        public static HttpResponse PostMod(HttpRequest request)
        {
            //仅在 StreamUpload 模式下，UploadFile 会被填充并包含请求的输入流
            if (request?.UploadFile == null || request.UploadFile.Stream == null)
            {
                return new HttpResponse(400, "缺少上传的文件流");
            }
            
            try
            {
                var upload = request.UploadFile;
                var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", "mods");
                Directory.CreateDirectory(uploadsDir);
                
                var fileName = string.IsNullOrEmpty(upload.FileName) ? $"upload_{DateTime.UtcNow.Ticks}" : upload.FileName;
                var filePath = Path.Combine(uploadsDir, fileName);
                
                // 将上传流保存到文件（同步写入以简化示例）
                using (var outFs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    upload.Stream.CopyTo(outFs);
                }
                
                Logger.Info($"已保存上传文件: {filePath}");
                
                return new HttpResponse(200, "上传成功");
            }
            catch (Exception ex)
            {
                Logger.Error($"处理上传时发生错误: {ex.Message}");
                return new HttpResponse(500, "上传处理失败");
            }
        }
    }
}
