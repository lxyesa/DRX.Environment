using Microsoft.AspNetCore.Mvc; // [ApiController] JSON
using KaxServer.Models; // StoreItem
using KaxServer.Services; // StoreManager, UserManager

namespace KaxServer.Controllers
{
    // 约定：纯 Web API，[ApiController]，前缀 api/store；购买从会话解析用户
    [ApiController]
    [Route("api/[controller]")]
    public class StoreController : ControllerBase
    {
        // GET: api/store/items
        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<StoreItem>>> GetAll()
        {
            var items = await StoreManager.GetAllStoreItemsAsync();
            return Ok(items);
        }

        // GET: api/store/items/{id}
        [HttpGet("items/{id:int}")]
        public async Task<ActionResult<StoreItem>> GetById([FromRoute] int id)
        {
            var item = await StoreManager.GetStoreItemByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        public record CreateStoreItemRequest(string Title, string Description, int OwnerId, int ItemId);

        // POST: api/store/items
        [HttpPost("items")]
        public async Task<ActionResult> Create([FromBody] CreateStoreItemRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Title))
                return BadRequest("invalid payload");

            var ok = await StoreManager.CreateStoreItemAsync(req.Title, req.Description ?? string.Empty, req.OwnerId, req.ItemId);
            if (!ok) return StatusCode(500, "create failed");
            return CreatedAtAction(nameof(GetById), new { id = req.ItemId }, null);
        }

        // PUT: api/store/items/{id}
        [HttpPut("items/{id:int}")]
        public async Task<ActionResult> Update([FromRoute] int id, [FromBody] StoreItem item)
        {
            if (item == null || id != item.Id) return BadRequest("id mismatch");
            var ok = await StoreManager.UpdateStoreItemAsync(item);
            if (!ok) return StatusCode(500, "update failed");
            return NoContent();
        }

        // DELETE: api/store/items/{id}
        [HttpDelete("items/{id:int}")]
        public async Task<ActionResult> Delete([FromRoute] int id)
        {
            var ok = await StoreManager.DeleteStoreItemAsync(id);
            if (!ok) return StatusCode(500, "delete failed");
            return NoContent();
        }

        // DELETE: api/store/items
        [HttpDelete("items")]
        public async Task<ActionResult> Clear()
        {
            var ok = await StoreManager.ClearStoreItemsAsync();
            if (!ok) return StatusCode(500, "clear failed");
            return NoContent();
        }

        // POST: api/store/items/{id}/buy?priceIndex=0
        [HttpPost("items/{id:int}/buy")]
        public async Task<ActionResult<BuyResult>> Buy([FromRoute] int id, [FromQuery] int priceIndex = 0)
        {
            // 从会话解析当前用户（按现有 UserManager 约定）
            var user = await UserManager.GetCurrentUserAsync(HttpContext);
            if (user == null) return Unauthorized();

            var result = await StoreManager.BuyItemAsync(user, id, priceIndex);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // GET: api/store/purchase/verify?token=xxxx
        // 一次性验证购买成功跳转令牌：成功则返回订单与用户信息，并立即销毁令牌
        [HttpGet("purchase/verify")]
        public ActionResult<object> Verify([FromQuery] string token)
        {
            if (!StoreManager.ConsumePurchaseToken(token, out var uid, out var itemId, out var orderId))
            {
                return Unauthorized(new { message = "无效或过期令牌" });
            }
            return Ok(new { userId = uid, itemId, orderId });
        }
    }
}