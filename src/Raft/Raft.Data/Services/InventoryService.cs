using System.Net.Http.Json;
using Raft.Data.Models;

namespace Raft.Data.Services;

public interface IInventoryService
{
    Task<Product> GetCurrentStockAsync(Product product);
    Task<Product> IncrementProductStockAsync(Product product);
    Task<Product> DecrementProductStockAsync(Product product);
}

public class InventoryService : IInventoryService
{
    private readonly HttpClient client;
    private readonly IStorageService storageService;

    public InventoryService(HttpClient client, IStorageService storageService)
    {
        this.client = client;
        this.storageService = storageService;
    }

    public async Task<Product> GetCurrentStockAsync(Product product)
    {
        var key = GetProductStockKey(product);

        var response = await storageService.EventualGet(key); 

        if (String.IsNullOrEmpty(response!.Value))
            return new Product(product.Id, product.Name, product.Description, product.Price, 0);

        int quantity;
        if (int.TryParse(response!.Value, out quantity))
            return new Product(product.Id, product.Name, product.Description, product.Price, quantity);


        throw new InvalidOperationException("Invalid quantity value");
    }

    public async Task<Product> IncrementProductStockAsync(Product product)
    {
       var reducer = new Func<string, string>(oldValue =>
        {
            var quantity = int.Parse(oldValue);
            quantity++;
            return quantity.ToString();
        });

        var key = GetProductStockKey(product);
        await storageService.IdempodentReduceUntilSuccess(key, product.QuantityInStock.ToString(), reducer);
        var optimisticProduct = new Product(product.Id, product.Name, product.Description, product.Price, product.QuantityInStock + 1);

        return optimisticProduct;
    }

    public async Task<Product> DecrementProductStockAsync(Product product)
    {
        var reducer = new Func<string, string>(oldValue =>
        {
            var quantity = int.Parse(oldValue);
            quantity--;
            return quantity.ToString();
        });

        var key = GetProductStockKey(product);
        await storageService.IdempodentReduceUntilSuccess(key, product.QuantityInStock.ToString(), reducer);
        var optimisticProduct = new Product(product.Id, product.Name, product.Description, product.Price, product.QuantityInStock - 1);
        return optimisticProduct;
    }

    private string GetProductStockKey(Product product)
    {
        return $"stock-of-{product.Name}";
    }

}
