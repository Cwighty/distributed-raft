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
    private readonly IStorageService storageService;

    public AccountService(HttpClient client, IStorageService storageService)
    {
        this.client = client;
        this.storageService = storageService;
    }


    public async Task DepositAsync(string username, decimal amount)
    {
        var key = GetAccountBalanceKey(username);
        var currentBalance = await GetBalanceAsync(username);

        var reducer = new Func<string, string>(oldValue =>
        {
            var balance = int.Parse(oldValue);
            balance += (int)amount;
            return balance.ToString();
        });

        await storageService.IdempodentReduceUntilSuccess(key, currentBalance.ToString(), reducer);
    }

    public async Task<int> GetBalanceAsync(string username)
    {
        var key = GetAccountBalanceKey(username);

        var response = await storageService.EventualGet(key);

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
