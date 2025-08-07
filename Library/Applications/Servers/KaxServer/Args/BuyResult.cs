public struct BuyResult
{
    public string? Message { get; set; }
    public bool Success { get; set; }
    public int ItemId { get; set; }
    public int UserId { get; set; }
    public int Price { get; set; }

    // 一次性购买完成跳转令牌（10分钟有效，首次验证即销毁）
    public string? Token { get; set; }

    // 前端跳转展示的订单号（可与 ItemId 不同，若无订单系统则用 ItemId 退化）
    public string? OrderId { get; set; }
}