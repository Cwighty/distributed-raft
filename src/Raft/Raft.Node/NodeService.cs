using Raft.Data.Models;
using Raft.Node.Options;

namespace Raft.Node;

enum NodeState
{
    Follower,
    Candidate,
    Leader
}

public class NodeService : BackgroundService
{
    private NodeState state = NodeState.Follower;
    private DateTime lastHeartbeatReceived;
    private int electionTimeout;
    private Random random = new Random();
    private List<string> otherNodeAddresses = new List<string>();

    public int Id { get; private set; }
    public Dictionary<string, VersionedValue<string>> Data { get; set; } = new();
    public int CurrentTerm { get; private set; } = 0;
    public int CommittedIndex { get; private set; }
    public int VotedFor { get; private set; }
    public int LeaderId { get; private set; }
    public bool IsLeader => state == NodeState.Leader;


    private readonly HttpClient client;
    private readonly ILogger<NodeService> logger;
    private readonly ApiOptions options;

    public NodeService(HttpClient client, ILogger<NodeService> logger, ApiOptions options)
    {
        this.client = client;
        this.logger = logger;
        this.options = options;

        Id = options.NodeIdentifier;
        electionTimeout = random.Next(150, 300);
        for (int i = 1; i <= options.NodeCount; i++)
        {
            if (i == options.NodeIdentifier)
            {
                continue;
            }
            otherNodeAddresses.Add($"http://node{i}:{options.NodeServicePort}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Initializing node.");
        if (Directory.Exists(options.EntryLogPath))
        {
            var logFiles = new DirectoryInfo(options.EntryLogPath).GetFiles().OrderBy(f => f.Name);
            foreach (var file in logFiles)
            {
                var index = int.Parse(file.Name.Split('.')[0]);
                var lines = File.ReadAllLines(file.FullName);
                Data[lines[1]] = new VersionedValue<string> { Value = lines[2], Version = index };
            }
            CommittedIndex = logFiles.Count();
        }

        lastHeartbeatReceived = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (state == NodeState.Leader)
            {
                Task.Delay(100).Wait();
                SendHeartbeats();
            }
            else if (HasElectionTimedOut())
            {
                Log("Election timed out.");
                await StartElection();
            }
        }
    }

    public (string value, int version) Get(string key)
    {
        return ("value", 1);
    }


    public async Task StartElection(int term = 0)
    {
        state = NodeState.Candidate;
        if (term > 0)
        {
            CurrentTerm = term;
        }
        else
        {
            CurrentTerm++;
        }
        ResetElectionTimeout();
        Log($"Running for election cycle {CurrentTerm}. Requesting votes from other nodes.");
        int votesReceived = 1; // vote for self
        VotedFor = Id;
        long myLatestCommittedLogIndex = 0;
        if (Data.Count() > 0)
            myLatestCommittedLogIndex = Data.Values.Max(v => v.Version);

        foreach (var nodeAddress in otherNodeAddresses)
        {
            VoteResponse? response = null;
            try
            {
                response = await RequestVoteAsync(nodeAddress, CurrentTerm, myLatestCommittedLogIndex);
            }
            catch (Exception ex)
            {
                Log($"Vote request failed at {nodeAddress}. {ex.Message}");
            }


            if (response != null && response.VoteGranted)
            {
                votesReceived++;
                Log($"Received vote from node #{response.VoterId} {votesReceived}/{options.NodeCount} votes received.");
            }
            else
            {
                Log($"Vote request denied at {nodeAddress}.");
            }
        }

        if (votesReceived > options.NodeCount / 2)
        {
            state = NodeState.Leader;
            LeaderId = Id;
            Log("Became the Leader.");
            SendHeartbeats();
        }
        else
        {
            state = NodeState.Follower;
            Log("Lost election.");
        }
    }

    private async Task<VoteResponse> RequestVoteAsync(string addr, int currentTerm, long myLatestCommittedLogIndex)
    {
        var request = new VoteRequest
        {
            CandidateId = Id,
            Term = currentTerm,
            LastLogIndex = myLatestCommittedLogIndex
        };

        var response = await client.PostAsJsonAsync($"{addr}/raft/request-vote", request);

        if (response.IsSuccessStatusCode)
        {
            var voteResponse = await response.Content.ReadFromJsonAsync<VoteResponse>();
            if (voteResponse != null)
            {
                return voteResponse;
            }
        }

        throw new Exception("Vote request failed.");
    }

    public bool VoteForCandidate(VoteRequest request)
    {
        return VoteForCandidate(request.CandidateId, request.Term, request.LastLogIndex);
    }

    public bool VoteForCandidate(int candidateId, int theirTerm, long theirCommittedLogIndex)
    {
        if (theirTerm < CurrentTerm || theirCommittedLogIndex < CommittedIndex)
        {
            Log($"Denied vote request from node {candidateId} in election cycle {theirTerm}.");
            return false;
        }

        if (theirTerm > CurrentTerm || (theirTerm == CurrentTerm && (VotedFor == 0 || VotedFor == candidateId)))
        {
            CurrentTerm = theirTerm;
            VotedFor = candidateId;
            state = NodeState.Follower;
            ResetElectionTimeout();
            Log($"Voted for node {candidateId} in election term {theirTerm}.");
            return true;
        }
        else
        {
            Log($"Denied vote request from node {candidateId} in election cycle {theirTerm}.");
            return false;
        }
    }

    private bool HasElectionTimedOut()
    {
        return DateTime.UtcNow - lastHeartbeatReceived > TimeSpan.FromMilliseconds(electionTimeout);
    }

    private void ResetElectionTimeout()
    {
        electionTimeout = random.Next(3000, 6500);
        lastHeartbeatReceived = DateTime.UtcNow;
    }

    private DateTime lastMessageClearTime = DateTime.MinValue;
    private HashSet<string> sentMessages = new HashSet<string>();

    private void Log(string message)
    {
        if (DateTime.UtcNow - lastMessageClearTime >= TimeSpan.FromSeconds(options.LogMessageIntervalSeconds))
        {
            sentMessages.Clear();
            lastMessageClearTime = DateTime.UtcNow;
        }
        if (!sentMessages.Contains(message))
        {
            logger.LogInformation($"{message}");
            sentMessages.Add(message);
        }
    }

    private async void SendHeartbeats()
    {
        foreach (var nodeAddress in otherNodeAddresses)
        {
            await RequestAppendEntriesAsync(nodeAddress, CurrentTerm, CommittedIndex, Data);
        }
    }

    private async Task RequestAppendEntriesAsync(string nodeAddress, int currentTerm, int committedIndex, Dictionary<string, VersionedValue<string>> data)
    {
        var request = new AppendEntriesRequest
        {
            LeaderId = Id,
            Term = currentTerm,
            LeaderCommittedIndex = committedIndex,
            Entries = data
        };

        try
        {
            var response = await client.PostAsJsonAsync($"{nodeAddress}/raft/append-entries", request);

            if (response.IsSuccessStatusCode)
            {
                Log($"Heartbeat sent | Term: {currentTerm} | Committed: {committedIndex} | Occupation: {state}");
            }
            else
            {
                Log("Heartbeat failed.");
            }
        }
        catch (Exception ex)
        {
            Log($"Heartbeat failed. {ex.Message}");
        }
    }

    public bool AppendEntry(AppendEntryRequest request)
    {
        return AppendEntry(request.Key, request.Value, request.Version, request.Term);
    }

    public bool AppendEntry(string key, string value, long logIndex, int term)
    {

        var mostRecentIndex = 0;
        if (Directory.Exists(options.EntryLogPath))
        {
            mostRecentIndex = new DirectoryInfo(options.EntryLogPath).GetFiles().Length;
        }

        if (logIndex > mostRecentIndex)
        {
            LogEntry(key, value, logIndex, term);
        }
        return true;
    }

    private void LogEntry(string key, string value, long index, int leaderTerm)
    {
        if (!Directory.Exists(options.EntryLogPath))
        {
            Directory.CreateDirectory(options.EntryLogPath);
        }

        var filePath = $"{options.EntryLogPath}/{index}.log";

        if (File.Exists(filePath))
        {
            return;
        }

        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(leaderTerm);
                writer.WriteLine(key);
                writer.WriteLine(value);
            }
        }

        Log($"Log entry added | Index: {index} | Term: {leaderTerm} | Key: {key} | Value: {value}");
    }

    public bool AppendEntries(AppendEntriesRequest request)
    {
        if (request.Term >= CurrentTerm)
        {
            ResetElectionTimeout();
            CurrentTerm = request.Term;
            state = NodeState.Follower;
            LeaderId = request.LeaderId;
            Log($"Heartbeat received | Term: {CurrentTerm} | Committed: {CommittedIndex} | Following: {LeaderId}");

            foreach (var entry in request.Entries)
            {
                var mostRecentIndex = 0;
                if (Directory.Exists(options.EntryLogPath))
                {
                    mostRecentIndex = new DirectoryInfo(options.EntryLogPath).GetFiles().Length;
                }
                var newEntries = request.Entries.Where(e => e.Value.Version > mostRecentIndex);
                LogEntry(entry.Key, entry.Value.Value, entry.Value.Version, request.Term);
            }

            if (request.LeaderCommittedIndex > CommittedIndex)
            {
                CommitLogs(request.LeaderCommittedIndex);
            }
            return true;
        }

        return false;
    }

    private void CommitLogs(int committedIndex)
    {
        // Commit logs up to the committed index
        if (Directory.Exists(options.EntryLogPath))
        {
            var logFiles = new DirectoryInfo(options.EntryLogPath).GetFiles().OrderBy(f => f.Name);
            foreach (var file in logFiles)
            {
                Log(file.Name);
                var index = int.Parse(file.Name.Split('.')[0]);
                if (index <= committedIndex)
                {
                    Log($"Committing index {index}.");
                    var lines = File.ReadAllLines(file.FullName);
                    Data[lines[1]] = new VersionedValue<string> { Value = lines[2], Version = index };
                }
                if (index > committedIndex)
                {
                    Log($"Deleting index {index}. Over elected majority committed index: {committedIndex}.");
                    file.Delete();
                }
            }
            CommittedIndex = committedIndex;
        }
    }

    public async Task<VersionedValue<string>> StrongGet(string key)
    {
        Log($"StrongGet called with key: {key}");
        if (!IsLeader)
        {
            throw new Exception("Not the leader.");
        }
        var confirmLeaderCount = 1;
        foreach (var nodeAddr in otherNodeAddresses)
        {
            if (await ConfirmLeader(nodeAddr))
            {
                confirmLeaderCount++;
            }
            if (confirmLeaderCount > options.NodeCount / 2)
            {
                if (Data.ContainsKey(key))
                {
                    return Data[key];
                }
                else
                {
                    throw new Exception("Value not found.");
                }
            }
        }
        throw new Exception("Not the leader.");
    }

    public async Task<bool> ConfirmLeader(string addr)
    {
        var leaderId = await client.GetFromJsonAsync<int>($"{addr}/raft/who-is-leader");
        if (leaderId == Id)
        {
            return true;
        }
        return false;
    }

    public VersionedValue<string> EventualGet(string key)
    {
        Log($"EventualGet called with key: {key}");

        if (Data.ContainsKey(key))
        {
            return Data[key];
        }

        return new VersionedValue<string> { Value = String.Empty, Version = 0 };
    }

    public async Task CompareAndSwap(string key, string? oldValue, string newValue)
    {
        if (!IsLeader)
        {
            throw new Exception("Not the leader.");
        }

        var newIndex = 1;
        if (Directory.Exists(options.EntryLogPath))
        {
            newIndex = new DirectoryInfo(options.EntryLogPath).GetFiles().Length + 1;
        }
        if (Data.ContainsKey(key) && oldValue != Data[key].Value)
            throw new Exception("Value does not match.");

        LogEntry(key, newValue, newIndex, CurrentTerm);
        if (await BroadcastReplication(key, newValue, newIndex))
        {
            // if majority of nodes have replicated the log, update the data
            Data[key] = new VersionedValue<string> { Value = newValue, Version = newIndex };
            CommittedIndex = newIndex;
            return;
        }
        throw new Exception("Could not replicate to majority of nodes.");
    }

    private async Task<bool> BroadcastReplication(string key, string value, int index)
    {
        var confirmReplicationCount = 1;
        foreach (var nodeAddr in otherNodeAddresses)
        {
            if (await RequestAppendEntry(nodeAddr, CurrentTerm, key, value, index))
            {
                confirmReplicationCount++;
            }
        }
        if (confirmReplicationCount > options.NodeCount / 2)
        {
            return true;
        }
        return false;
    }

    private async Task<bool> RequestAppendEntry(string addr, int term, string key, string value, int index)
    {
        var request = new AppendEntryRequest
        {
            Term = term,
            Key = key,
            Value = value,
            Version = index
        };

        var response = await client.PostAsJsonAsync($"{addr}/raft/append-entry", request);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        return false;
    }
}
