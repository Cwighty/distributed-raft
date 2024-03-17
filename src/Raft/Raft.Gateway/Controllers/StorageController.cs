using Microsoft.AspNetCore.Mvc;
using Raft.Data.Models;
using Raft.Gateway.Options;

namespace Raft.Gateway.Controllers;

[ApiController]
[Route("gateway/[controller]")]
public class StorageController : ControllerBase
{
    private readonly ILogger<StorageController> _logger;
    private readonly ApiOptions options;
    private readonly HttpClient client;
    private List<string> nodeAddresses = new List<string>();

    public StorageController(ILogger<StorageController> logger, ApiOptions options, HttpClient client)
    {
        _logger = logger;
        this.options = options;
        this.client = client;
        InitializeNodeAddresses(options);
    }

    private void InitializeNodeAddresses(ApiOptions options)
    {
        nodeAddresses = [];
        for (var i = 1; i <= options.NodeCount; i++)
        {
            nodeAddresses.Add($"http://{options.NodeServiceName}{i}:{options.NodeServicePort}");
        }
    }

    [HttpGet("StrongGet")]
    public async Task<ActionResult<VersionedValue<string>>> StrongGet([FromQuery] string key)
    {
        _logger.LogInformation("StrongGet called with key: {key}", key);
        var leaderAddress = FindLeaderAddress();
        var value = await client.GetFromJsonAsync<VersionedValue<string>>($"{leaderAddress}/Storage/StrongGet?key={key}");
        if (value == null)
        {
            throw new Exception("Value not found");
        }
        return value;
    }

    [HttpGet("EventualGet")]
    public async Task<ActionResult<VersionedValue<string>>> EventualGet([FromQuery] string key)
    {
        _logger.LogInformation("EventualGet called with key: {key}", key);
        var randomAddress = GetRandomNodeAddress();
        var value = await client.GetFromJsonAsync<VersionedValue<string>>($"{randomAddress}/Storage/EventualGet?key={key}");
        if (value == null)
        {
            throw new Exception("Value not found");
        }
        return value;
    }

    [HttpPost("CompareAndSwap")]
    public async Task<ActionResult> CompareAndSwap(CompareAndSwapRequest request)
    {
        _logger.LogInformation("CompareAndSwap called with key: {key}, oldValue: {oldValue}, newValue: {newValue}", request.Key, request.OldValue, request.NewValue);
        var leaderAddress = FindLeaderAddress();
        var response = await client.PostAsJsonAsync($"{leaderAddress}/Storage/CompareAndSwap", request);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("Failed to compare and swap, key: {key}, oldValue: {oldValue}, newValue: {newValue}", request.Key, request.OldValue, request.NewValue);
            return BadRequest();
        }
        return Ok();
    }

    private string FindLeaderAddress()
    {
        var address = GetRandomNodeAddress();

        var leaderId = 0;
        while (leaderId == 0)
        {
            leaderId = GetLeaderId(address).Result;
            if (leaderId == 0)
            {
                address = GetRandomNodeAddress();
            }
        }

        return nodeAddresses.First(a => a.Contains(leaderId.ToString()));
    }

    private async Task<int> GetLeaderId(string address)
    {
        var response = await client.GetAsync($"{address}/Raft/who-is-leader");
        response.EnsureSuccessStatusCode();
        var leaderId = await response.Content.ReadAsStringAsync();
        return int.Parse(leaderId);
    }

    private string GetRandomNodeAddress()
    {
        var random = new Random();
        return nodeAddresses[random.Next(0, nodeAddresses.Count)];
    }
}
