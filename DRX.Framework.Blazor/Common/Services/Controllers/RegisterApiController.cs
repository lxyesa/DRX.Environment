using DRX.Framework.Common.Enums.Packet;
using DRX.Framework.Common.Models;
using DRX.Framework.Common.Utility;
using Microsoft.AspNetCore.Mvc;

namespace DRX.Framework.Blazor.Common.Services.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class RegisterApiController : ControllerBase
    {
        [HttpGet("register")]
        public ActionResult<DRXPacket> GetRegisterPacket()
        {
            var packet = new DRXPacket
            {
                Action = "Register"
            };

            var packed = packet.TryGenerateHash(DrxFile.ReadJsonKey<string>(DrxFile.ConfigPath, "Key"));

            return Ok(packet.Pack(DrxFile.ReadJsonKey<string>(DrxFile.ConfigPath, "Key")));
        }

        [HttpPost("register")]
        public ActionResult<DRXPacket> PostRegisterPacket([FromBody] byte[] data)
        {
            try
            {
                // 假设密钥为16位
                string? key = DrxFile.ReadJsonKey<string?>(DrxFile.ConfigPath, "Key");
                var packet = DRXPacket.Unpack(data, key);

                if (packet == null)
                {
                    return BadRequest("Invalid packet data.");
                }

                // 处理数据包
                packet.Action = "RegisterProcessed";

                return Ok(packet);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing packet: {ex.Message}");
            }
        }
    }
}
