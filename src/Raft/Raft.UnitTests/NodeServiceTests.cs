namespace Raft.UnitTests;

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Raft.Data.Models;
using Raft.Node;
using Raft.Node.Options;
using Raft.Node.Services;

[TestFixture]
public class NodeServiceTests
{

    // The node initializes correctly and reads existing log files if they exist.
    [Test]
    public void Node_initialization()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        // Act
        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);

        // Assert
        Assert.NotNull(nodeService);
        Assert.That(nodeService.State, Is.EqualTo(NodeState.Follower));
        Assert.That(nodeService.CurrentTerm, Is.EqualTo(0));
        Assert.That(nodeService.CommittedIndex, Is.EqualTo(0));
        Assert.That(nodeService.VotedFor, Is.EqualTo(0));
        Assert.False(nodeService.IsLeader);
    }


    // The node sends heartbeats when it is the leader.
    [Test]
    public void Node_sends_heartbeats_when_leader()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object)
        {
            State = NodeState.Leader
        };

        // Act
        nodeService.SendHeartbeats();

        // Assert
        nodeClientMock.Verify(nc => nc.RequestAppendEntriesAsync(It.IsAny<string>(), It.IsAny<AppendEntriesRequest>()), Times.Exactly(nodeService.OtherNodeAddresses.Count));
    }

    // The node starts an election when the election timeout has passed.
    [Test]
    public async Task Node_starts_election_when_timeout_passed()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var stoppingTokenSource = new CancellationTokenSource();
        var stoppingToken = stoppingTokenSource.Token;

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);

        // Act
        await nodeService.StartElection();

        // Assert
        Assert.That(nodeService.State, Is.EqualTo(NodeState.Follower)); // return to follower state when lost election
        Assert.True(nodeService.CurrentTerm > 0);
        Assert.True(nodeService.VotedFor == nodeService.Id);
    }

    // The node receives votes from other nodes and becomes the leader if it receives votes from the majority.
    [Test]
    public async Task Node_becomes_leader()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);

        // Set up otherNodeAddresses to have a majority of nodes
        nodeService.OtherNodeAddresses = new List<string> { "http://node1:8080", "http://node2:8080", "http://node3:8080" };

        // Set up RequestVoteAsync to return a granted vote
        nodeClientMock.Setup(x => x.RequestVoteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(new VoteResponse { VoteGranted = true });

        // Act
        await nodeService.StartElection();

        // Assert
        Assert.That(nodeService.State, Is.EqualTo(NodeState.Leader));
    }

    // The node denies vote requests from other nodes if their term or committed log index is lower than its own.
    [Test]
    public void Node_denies_vote_requests()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();
        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        nodeService.CurrentTerm = 2;
        nodeService.CommittedIndex = 3;

        // Act
        var result = nodeService.VoteForCandidate(1, 1, 2);

        // Assert
        Assert.False(result);
    }

    // The node appends an entry to its log if the log index is greater than the most recent index.
    [Test]
    public void Node_appends_entry_to_log()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        var key = "key";
        var value = "value";
        var logIndex = 1;
        var term = 1;

        // Act
        var result = nodeService.AppendEntry(key, value, logIndex, term);

        // Assert
        Assert.True(result);
    }

    // The node votes for a candidate if their term is higher or if their term is the same and it has not voted for another candidate.
    [Test]
    public void Node_voting()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        nodeService.State = NodeState.Follower;
        nodeService.CurrentTerm = 1;
        nodeService.VotedFor = 0;
        nodeService.CommittedIndex = 0;

        // Act
        var result1 = nodeService.VoteForCandidate(2, 2, 0); // term is higher
        var result2 = nodeService.VoteForCandidate(2, 1, 0); // term is lower so deny

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }

    // The node performs an eventual get operation and returns the value if it exists in its data.
    [Test]
    public void Node_eventual_get()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();
        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        var key = "key";
        var value = "value";
        var version = 1;
        nodeService.Data[key] = new VersionedValue<string> { Value = value, Version = version };

        // Act
        var result = nodeService.EventualGet(key);

        // Assert
        Assert.NotNull(result);
        Assert.That(result.Value, Is.EqualTo(value));
        Assert.That(result.Version, Is.EqualTo(version));
    }

    // The node commits logs up to the leader's committed index when receiving append entries.
    [Test]
    public void Node_commits_logs_to_leader_committed_index()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        nodeService.State = NodeState.Leader;
        nodeService.CurrentTerm = 1;
        nodeService.CommittedIndex = 0;
        nodeService.LeaderId = 1;

        var appendEntriesRequest = new AppendEntriesRequest
        {
            Term = 1,
            LeaderId = 1,
            LeaderCommittedIndex = 2,
            Entries = new Dictionary<string, VersionedValue<string>>
        {
            { "key1", new VersionedValue<string> { Value = "value1", Version = 1 } },
            { "key2", new VersionedValue<string> { Value = "value2", Version = 2 } }
        }
        };

        // Act
        var result = nodeService.AppendEntries(appendEntriesRequest);

        // Assert
        Assert.True(result);
        Assert.That(nodeService.CommittedIndex, Is.EqualTo(2));
    }

    // The node performs a strong get operation and returns the value if it is the leader and the majority of nodes confirm it as the leader.
    [Test]
    public async Task Node_performs_strong_get_operation()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NodeService>>();
        var apiOptions = new ApiOptions();
        var nodeClientMock = new Mock<INodeClient>();

        var nodeService = new NodeService(loggerMock.Object, apiOptions, nodeClientMock.Object);
        nodeService.State = NodeState.Leader;
        nodeService.LeaderId = nodeService.Id;
        nodeService.Data["key"] = new VersionedValue<string> { Value = "value", Version = 1 };
        nodeService.CommittedIndex = 1;

        var otherNodeAddresses = new List<string> { "http://node2:8080", "http://node3:8080" };

        nodeClientMock.SetupSequence(x => x.ConfirmLeaderAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        // Act
        var result = await nodeService.StrongGet("key");

        // Assert
        Assert.NotNull(result);
        Assert.That(result.Value, Is.EqualTo("value"));
        Assert.That(result.Version, Is.EqualTo(1));
    }

}
