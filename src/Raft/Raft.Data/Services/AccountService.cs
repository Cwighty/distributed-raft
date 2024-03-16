using System.Net.Http.Json;
using Raft.Data.Models;

namespace Raft.Data.Services;

public interface IAccountService
{
    Task DepositAsync(string username, decimal amount);
    Task<int> GetBalanceAsync(string username);
}

public class AccountService : IAccountService
{
    private readonly HttpClient client;

    public AccountService(HttpClient client)
    {
        this.client = client;
    }


    public async Task DepositAsync(string username, decimal amount)
    {
        var key = GetAccountBalanceKey(username);
        var currentBalance = await GetBalanceAsync(username);
        var newBalance = currentBalance + amount;

        var request = new CompareAndSwapRequest
        {
            Key = key,
            OldValue = currentBalance.ToString(),
            NewValue = newBalance.ToString()
        };

        await client.PostAsJsonAsync($"gateway/Storage/CompareAndSwap", request);
    }

    public async Task<int> GetBalanceAsync(string username)
    {
        var key = GetAccountBalanceKey(username);

        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/EventualGet?key={key}");

        if (String.IsNullOrEmpty(response!.Value))
            return 0;

        int balance;
        if (int.TryParse(response!.Value, out balance))
            return balance;

        throw new InvalidOperationException("Invalid balance value");
    }

    private string GetAccountBalanceKey(string username)
    {
        return $"account_balance-{username}";
    }

}
