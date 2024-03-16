using Raft.Data.Models;

namespace Raft.Node.Services;

public interface INodeClient
{
    Task<VoteResponse> RequestVoteAsync(string address, int term, long lastLogIndex, int candidateId);
    Task<bool> RequestAppendEntriesAsync(string address, AppendEntriesRequest request);
    Task<bool> ConfirmLeaderAsync(string address, int leaderId);
    Task<bool> RequestAppendEntryAsync(string address, int currentTerm, string key, string value, int index);
}

public class NodeClient : INodeClient
{
    private readonly HttpClient _client;
    private readonly ILogger<NodeClient> _logger;

    public NodeClient(HttpClient client, ILogger<NodeClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<VoteResponse> RequestVoteAsync(string address, int term, long lastLogIndex, int candidateId)
    {
        var request = new VoteRequest
        {
            CandidateId = candidateId,
            Term = term,
            LastLogIndex = lastLogIndex
        };

        try
        {
            var response = await _client.PostAsJsonAsync($"{address}/raft/request-vote", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VoteResponse>() ?? throw new Exception("Vote response was null");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Vote request to {address} failed: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> RequestAppendEntriesAsync(string address, AppendEntriesRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync($"{address}/raft/append-entries", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Append entries request to {address} failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ConfirmLeaderAsync(string address, int leaderId)
    {
        try
        {
            var leaderIdResponse = await _client.GetFromJsonAsync<int>($"{address}/raft/who-is-leader");
            return leaderId == leaderIdResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Confirm leader request to {address} failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RequestAppendEntryAsync(string address, int term, string key, string value, int index)
    {

        var request = new AppendEntryRequest
        {
            Term = term,
            Key = key,
            Value = value,
            Version = index
        };

        try
        {
            var response = await _client.PostAsJsonAsync($"{address}/raft/append-entry", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Append entry request to {address} failed: {ex.Message}");
            return false;
        }
    }
}
