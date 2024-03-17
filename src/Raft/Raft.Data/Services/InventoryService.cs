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

    public InventoryService(HttpClient client)
    {
        this.client = client;
    }


    private async Task<Product> SetCurrentStockAsync(Product product, int newStockValue)
    {
        var request = new CompareAndSwapRequest
        {
            Key = GetProductStockKey(product),
            OldValue = product.QuantityInStock.ToString(),
            NewValue = newStockValue.ToString()
        };
        var response = await client.PostAsJsonAsync($"gateway/Storage/CompareAndSwap", request);
        response.EnsureSuccessStatusCode();
        return new Product(product.Id, product.Name, product.Description, product.Price, newStockValue);
    }

    public async Task<Product> GetCurrentStockAsync(Product product)
    {
        var key = GetProductStockKey(product);

        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/EventualGet?key={key}");

        if (String.IsNullOrEmpty(response!.Value))
            return new Product(product.Id, product.Name, product.Description, product.Price, 0);

        int quantity;
        if (int.TryParse(response!.Value, out quantity))
            return new Product(product.Id, product.Name, product.Description, product.Price, quantity);


        throw new InvalidOperationException("Invalid quantity value");
    }

    public async Task<Product> IncrementProductStockAsync(Product product)
    {
        try
        {
            return await SetCurrentStockAsync(product, product.QuantityInStock + 1);
        }
        catch
        {
            // Retry logic
            int retryCount = 5;
            int currentRetry = 0;
            int delay = 1000; // milliseconds

            while (currentRetry < retryCount)
            {
                try
                {
                    product = await GetCurrentStockAsync(product);
                    return await SetCurrentStockAsync(product, product.QuantityInStock + 1);
                }
                catch
                {
                    // Increment the retry count
                    currentRetry++;

                    // Delay before retrying
                    await Task.Delay(delay);
                }
            }

            throw new Exception("Failed to set current stock");
        }
    }

    public async Task<Product> DecrementProductStockAsync(Product product)
    {
        try
        {
            return await SetCurrentStockAsync(product, product.QuantityInStock - 1);
        }
        catch
        {
            // Retry logic
            int retryCount = 5;
            int currentRetry = 0;
            int delay = 1000; // milliseconds

            while (currentRetry < retryCount)
            {
                try
                {
                    product = await GetCurrentStockAsync(product);
                    return await SetCurrentStockAsync(product, product.QuantityInStock - 1);
                }
                catch
                {
                    // Increment the retry count
                    currentRetry++;

                    // Delay before retrying
                    await Task.Delay(delay);
                }
            }

            throw new Exception("Failed to set current stock");
        }
    }


    private string GetProductStockKey(Product product)
    {
        return $"stock-of-{product.Name}";
    }

}
