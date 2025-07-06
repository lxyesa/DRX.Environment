using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Drx.Sdk.Network.Session;
using Drx.Sdk.Network.Email;
using Web.KaxServer.Models;
using Web.KaxServer.Services;

namespace Web.KaxServer.Controllers
{
    [ApiController]
    [Route("api/")]
    public class ApiController : Controller
    {
        private readonly SessionManager _sessionManager;
        private readonly IUserService _userService;

        public ApiController(SessionManager sessionManager, IUserService userService)
        {
            _sessionManager = sessionManager;
            _userService = userService;
        }

        [HttpGet("user/getname")]
        public IActionResult GetUserName(int userid)
        {
            var user = _userService.GetUserById(userid);
            if (user == null)
            {
                /* ret code 404 */
                return NotFound();
            }
            return Ok(user.Username);
        }
    }
}
