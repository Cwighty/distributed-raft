namespace Raft.Data.Models;

public enum OrderStatus
{
    Pending,
    Failed,
    Processed,
}

public class OrderInfo
{
    public string Username { get; set; } = "";
    public List<Product> Products { get; set; } = new List<Product>();
}


public class Order
{
    public Guid Id { get; set; }
    public OrderStatus? Status { get; set; }
    public OrderInfo? OrderInfo { get; set; }
}
