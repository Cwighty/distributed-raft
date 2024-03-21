namespace Raft.Data.Models;

public class OrderInfo
{
    public string Username { get; set; } = "";
    public List<Product> Products { get; set; } = new List<Product>();
    public decimal GetTotal() => Products.Sum(p => p.Price);
}


public class Order
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "pending";
    public OrderInfo? OrderInfo { get; set; }
}
