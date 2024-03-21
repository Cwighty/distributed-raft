using System.Text.Json;
using Raft.Data.Models;

namespace Raft.Data.Services;

public interface IOrderService
{
    Task<List<Order>> GetOrdersAsync();
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<Guid> CreateOrderAsync(OrderInfo orderInfo);
    Task<bool> ProcessOrderAsync(Guid orderId);
}

public class OrderService : IOrderService
{
    private readonly IStorageService storageService;
    private readonly IAccountService accountService;
    private readonly IInventoryService inventoryService;
    private string processorId = Guid.NewGuid().ToString();

    public OrderService(IStorageService storageService, IAccountService accountService, IInventoryService inventoryService)
    {
        this.storageService = storageService;
        this.accountService = accountService;
        this.inventoryService = inventoryService;
    }

    public async Task<List<Order>> GetOrdersAsync()
    {
        var orderListKey = GetOrderListKey();
        var orderIdsVersionedValue = await storageService.StrongGet(orderListKey);

        if (String.IsNullOrEmpty(orderIdsVersionedValue.Value))
            return new List<Order>();

        var orderIds = JsonSerializer.Deserialize<List<Guid>>(orderIdsVersionedValue.Value);

        var orders = new List<Order>();
        foreach (var orderId in orderIds!)
        {
            var orderInfo = await GetOrderInfoAsync(orderId);
            var orderStatus = await GetOrderStatusAsync(orderId);
            orders.Add(new Order
            {
                Id = orderId,
                OrderInfo = orderInfo,
                Status = orderStatus
            });
        }

        return orders;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        var orderInfo = await GetOrderInfoAsync(orderId);
        var orderStatus = await GetOrderStatusAsync(orderId);
        if (orderInfo == null || orderStatus == null)
            return null;
        return new Order
        {
            Id = orderId,
            OrderInfo = orderInfo,
            Status = orderStatus
        };
    }

    public async Task<Guid> CreateOrderAsync(OrderInfo orderInfo)
    {
        var orderId = Guid.NewGuid();
        var orderListKey = GetOrderListKey();
        var orderListVersionedValue = await storageService.StrongGet(orderListKey);

        if (String.IsNullOrEmpty(orderListVersionedValue.Value))
        {
            var orderIds = new List<Guid> { orderId };
            await AddOrderIdToOrderList(orderId, orderIds);
        }
        else
        {
            var orderIds = JsonSerializer.Deserialize<List<Guid>>(orderListVersionedValue.Value);
            await AddOrderIdToOrderList(orderId, orderIds!);
        }

        var orderInfoKey = GetOrderInfoKey(orderId);
        var orderInfoValue = JsonSerializer.Serialize(orderInfo);
        await storageService.IdempodentReduceUntilSuccess(orderInfoKey, "", (_) => orderInfoValue);

        await UpdateOrderStatusAsync(orderId, "pending");

        return orderId;
    }

    private async Task AddOrderIdToOrderList(Guid orderId, List<Guid> unmodifiedOrderIds)
    {
        var orderListKey = GetOrderListKey();
        var unmodifiedValue = JsonSerializer.Serialize(unmodifiedOrderIds);

        var orderListReducer = (string oldValue) =>
        {
            var orderIds = JsonSerializer.Deserialize<List<Guid>>(oldValue);
            orderIds!.Add(orderId);
            var newValue = JsonSerializer.Serialize(orderIds);
            return newValue;
        };

        await storageService.IdempodentReduceUntilSuccess(orderListKey, unmodifiedValue, orderListReducer);
    }

    private async Task<OrderInfo?> GetOrderInfoAsync(Guid orderId)
    {
        var orderInfoKey = GetOrderInfoKey(orderId);
        var orderInfoVersionedValue = await storageService.StrongGet(orderInfoKey);
        if (String.IsNullOrEmpty(orderInfoVersionedValue.Value))
            return null;
        return JsonSerializer.Deserialize<OrderInfo>(orderInfoVersionedValue.Value);
    }

    private async Task<string> GetOrderStatusAsync(Guid orderId)
    {
        var orderStatusKey = GetOrderStatusKey(orderId);
        var orderStatusVersionedValue = await storageService.StrongGet(orderStatusKey);
        return orderStatusVersionedValue.Value;
    }

    public async Task<bool> ProcessOrderAsync(Guid orderId)
    {
        var processorId = Guid.NewGuid().ToString();
        var order = await GetOrderAsync(orderId);
        if (order == null || order.OrderInfo == null)
        {
            return false;
        }

        bool alreadyProcessed = false;
        bool isBalanceUpdated = false;
        int isStockUpdated = 0;
        bool isVendorBalanceUpdated = false;

        try
        {
            // Step 1: Deduct User's Balance
            Console.WriteLine($"Checking balance for {order.OrderInfo.Username}");
            var userBalance = await accountService.GetBalanceAsync(order.OrderInfo.Username);
            if (userBalance >= order.OrderInfo.GetTotal())
            {
                await accountService.WithdrawAsync(order.OrderInfo.Username, order.OrderInfo.GetTotal());
                isBalanceUpdated = true;
            }
            else
            {
                throw new Exception("Insufficient funds.");
            }

            // Step 2: Deduct Stock for Each Product
            Console.WriteLine("Checking stock for each product");
            foreach (var item in order.OrderInfo.Products)
            {
                var product = await inventoryService.GetCurrentStockAsync(item);
                if (product.QuantityInStock >= 1)
                {
                    await inventoryService.DecrementProductStockAsync(item);
                }
                else
                {
                    throw new Exception("Insufficient stock.");
                }
                isStockUpdated += 1;
            }

            // Step 3: Increase Vendor's Balance
            Console.WriteLine("Depositing to vendor's account");
            await accountService.DepositAsync("vendor", order.OrderInfo.GetTotal());
            isVendorBalanceUpdated = true;

            // Step 4: Update Order Status
            Console.WriteLine("Updating order status");
            var orderStatus = await GetOrderStatusAsync(orderId);
            Console.WriteLine($"Order status: {orderStatus}");
            if (orderStatus == "pending")
            {
                await UpdateOrderStatusAsync(orderId, $"processed-by-{processorId}");
            }
            else
            {
                alreadyProcessed = true;
                throw new Exception("Order already processed.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing order: {ex.Message}");
            if (isVendorBalanceUpdated)
            {
                Console.WriteLine("UNDO: Withdrawing from vendor's account");
                await accountService.WithdrawAsync("vendor", order.OrderInfo.GetTotal());
            }
            if (isStockUpdated > 0)
            {
                Console.WriteLine("UNDO: Incrementing stock for each product");
                foreach (var item in order.OrderInfo.Products)
                {
                    if (isStockUpdated == 0)
                        break;
                    await inventoryService.IncrementProductStockAsync(item);
                    isStockUpdated -= 1;
                }
            }
            if (isBalanceUpdated)
            {
                Console.WriteLine("UNDO: Depositing to user's account");
                await accountService.DepositAsync(order.OrderInfo.Username, order.OrderInfo.GetTotal());
            }

            if (!alreadyProcessed)
            {
                Console.WriteLine("Failing order");
                await UpdateOrderStatusAsync(orderId, $"rejected-by-{processorId}");
            }

            return false;
        }
    }

    private async Task UpdateOrderStatusAsync(Guid orderId, string status)
    {
        var orderStatusKey = GetOrderStatusKey(orderId);
        await storageService.IdempodentReduceUntilSuccess(orderStatusKey, "", (_) => status);
    }

    private string GetOrderListKey()
    {
        return "orders";
    }

    private string GetOrderStatusKey(Guid orderId)
    {
        return $"order:{orderId}:status";
    }

    private string GetOrderInfoKey(Guid orderId)
    {
        return $"order:{orderId}:info";
    }
}
