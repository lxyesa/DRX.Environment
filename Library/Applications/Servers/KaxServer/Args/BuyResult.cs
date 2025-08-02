public struct BuyResult
{
    public string? Message { get; set; }
    public bool Success { get; set; }
    public int ItemId { get; set; }
    public int UserId { get; set; }
    public int Price { get; set; }
}