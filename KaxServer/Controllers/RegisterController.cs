using DRX.Framework.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace KaxServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        [HttpPost("register")]
        public IActionResult Register([FromBody] DRXPacket? packet)
        {
            if (packet == null)
            {
                return BadRequest("Invalid packet");
            }

            // 处理接收到的 packet
            packet.Action = "Register";
            packet.Data["message"] = "Registration successful";
            packet.State["code"] = "200";

            var pPacked = packet.Pack(Globals._key);
            return Ok(pPacked);
        }
    }
}
