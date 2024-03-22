using System.Net.Http.Json;
using Raft.Data.Models;

namespace Raft.Data.Services;

public interface IStorageService
{
    Task<VersionedValue<string>> StrongGet(string key);
    Task<VersionedValue<string>> EventualGet(string key);
    Task CompareAndSwap(string key, string oldValue, string newValue);
    Task ReduceValue(string key, string oldValue, Func<string, string> reducer);
    Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000);
}

public class StorageService : IStorageService
{
    private HttpClient client;

    public StorageService(HttpClient client)
    {
        this.client = client;
    }
    public async Task<VersionedValue<string>> StrongGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/StrongGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string> { Value = "", Version = 0 };
        return response;
    }

    public async Task<VersionedValue<string>> EventualGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/EventualGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string> { Value = "", Version = 0 };
        return response;
    }

    public async Task CompareAndSwap(string key, string oldValue, string newValue)
    {
        var request = new CompareAndSwapRequest
        {
            Key = key,
            OldValue = oldValue,
            NewValue = newValue
        };
        var response = await client.PostAsJsonAsync($"gateway/Storage/CompareAndSwap", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReduceValue(string key, string oldValue, Func<string, string> reducer)
    {
        var newValue = reducer(oldValue);
        await CompareAndSwap(key, oldValue, newValue);
    }

    public async Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000)
    {
        int currentRetry = 0;
        while (currentRetry < retryCount)
        {
            try
            {
                await ReduceValue(key, oldValue, reducer);
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(delay);
                var refetchedValue = await StrongGet(key);
                oldValue = refetchedValue.Value;
                currentRetry++;
            }
        }
        throw new InvalidOperationException("Failed to reduce value, retry limit exceeded");
    }
}
