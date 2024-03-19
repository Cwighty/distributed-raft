using System.Text.Json;
using Raft.Data.Models;

namespace Raft.Data.Services;

public interface IOrderService
{
    Task<List<Order>> GetOrdersAsync();
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<Guid> CreateOrderAsync(OrderInfo orderInfo);
}

public class OrderService : IOrderService
{
    private readonly IStorageService storageService;

    public OrderService(IStorageService storageService)
    {
        this.storageService = storageService;
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

        var orderStatusKey = GetOrderStatusKey(orderId);
        var orderStatusValue = JsonSerializer.Serialize(OrderStatus.Pending);
        await storageService.IdempodentReduceUntilSuccess(orderStatusKey, "", (_) => orderStatusValue);

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

    private async Task<OrderStatus?> GetOrderStatusAsync(Guid orderId)
    {
        var orderStatusKey = GetOrderStatusKey(orderId);
        var orderStatusVersionedValue = await storageService.StrongGet(orderStatusKey);
        if (String.IsNullOrEmpty(orderStatusVersionedValue.Value))
            return null;
        return JsonSerializer.Deserialize<OrderStatus>(orderStatusVersionedValue.Value);
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
