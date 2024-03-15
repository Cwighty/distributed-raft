namespace Raft.UnitTests;

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Raft.Data.Models;
using Raft.Node;
using Raft.Node.Options;

[TestFixture]
public class NodeServiceTests
{
    private NodeService nodeService;

    [SetUp]
    public void Setup()
    {
        var client = new HttpClient();
        var logger = new NullLogger<NodeService>();
        var options = new ApiOptions { NodeIdentifier = 1, NodeCount = 3, NodeServicePort = 5000, EntryLogPath = "./entrylogs"};
        nodeService = new NodeService(client, logger, options);
    }

    [Test]
    public void TheNodeInitializesWithDefaultStateAsFollower()
    {
        var client = new HttpClient();
        var logger = new NullLogger<NodeService>();
        var options = new ApiOptions { NodeIdentifier = 1, NodeCount = 3, NodeServicePort = 5000 };
        var nodeService = new NodeService(client, logger, options);

        Assert.That(nodeService.State, Is.EqualTo(NodeState.Follower));
    }

    [Test]
    public void StartElection_ChangesStateAndIncrementsTerm()
    {
        var initialTerm = nodeService.CurrentTerm;
        nodeService.StartElection().Wait();

        // node state stays follower when losing election
        Assert.That(nodeService.State, Is.EqualTo(NodeState.Follower));
        Assert.That(nodeService.CurrentTerm, Is.EqualTo(initialTerm + 1));
    }

    [Test]
    public void VoteForCandidate_GrantsVoteBasedOnTermAndLogIndex()
    {
        var request = new VoteRequest { CandidateId = 2, Term = nodeService.CurrentTerm + 1, LastLogIndex = nodeService.CommittedIndex };
        var result = nodeService.VoteForCandidate(request);

        Assert.IsTrue(result);
        Assert.That(nodeService.VotedFor, Is.EqualTo(request.CandidateId));
        Assert.That(nodeService.CurrentTerm, Is.EqualTo(request.Term));
    }

    [Test]
    public void AppendEntries_AppendsEntriesAndUpdatesTermAndLeaderIdBasedOnRequestTerm()
    {
        var initialTerm = nodeService.CurrentTerm;
        var request = new AppendEntriesRequest
        {
            Term = initialTerm + 1,
            LeaderId = 2,
            LeaderCommittedIndex = nodeService.CommittedIndex + 1,
            Entries = new Dictionary<string, VersionedValue<string>> { { "key", new VersionedValue<string> { Value = "value", Version = 1 } } }
        };

        var result = nodeService.AppendEntries(request);

        Assert.IsTrue(result);
        Assert.That(nodeService.CurrentTerm, Is.EqualTo(request.Term));
        Assert.That(nodeService.LeaderId, Is.EqualTo(request.LeaderId));
        Assert.IsTrue(nodeService.Data.ContainsKey("key"));
    }

    [Test]
    public async Task TheLeaderAsksEveryOtherNodeForVoteDuringElection()
    {
        var loggerMock = new Mock<ILogger<NodeService>>();
        var options = new ApiOptions { NodeIdentifier = 1, NodeCount = 3, NodeServicePort = 5000 };
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(""),
        };
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        var httpClient = new HttpClient(handlerMock.Object);
        var nodeService = new NodeService(httpClient, loggerMock.Object, options);

        await nodeService.StartElection();

        handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public void IfNotTheLeaderDenyStrongGetRequest()
    {
        var client = new HttpClient();
        var logger = new NullLogger<NodeService>();
        var options = new ApiOptions { NodeIdentifier = 1, NodeCount = 3, NodeServicePort = 5000 };
        var nodeService = new NodeService(client, logger, options);
        nodeService.Data["key"] = new VersionedValue<string> { Value = "value", Version = 1 };

        Assert.ThrowsAsync<Exception>(async () => await nodeService.StrongGet("key"));
    }
}
