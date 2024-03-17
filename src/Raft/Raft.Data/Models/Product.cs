namespace Raft.Data.Models;

public class Product
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; } = 1.0M;
    public int QuantityInStock { get; private set; }

    public Product(int id, string name, string description, decimal price = 1.0M, int quantityInStock = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        QuantityInStock = quantityInStock;
    }
}
