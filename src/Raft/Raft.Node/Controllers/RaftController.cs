using Microsoft.AspNetCore.Mvc;
using Raft.Data.Models;

namespace Raft.Node.Controllers;

[ApiController]
[Route("[controller]")]
public class RaftController : ControllerBase
{
    private readonly ILogger<RaftController> _logger;
    private readonly NodeService node;

    public RaftController(ILogger<RaftController> logger, NodeService node)
    {
        _logger = logger;
        this.node = node;
    }

    [HttpPost("append-entries")]
    public IActionResult AppendEntriesHeartbeat(AppendEntriesRequest request)
    {
        node.AppendEntries(request);
        return Ok();
    }

    [HttpPost("append-entry")]
    public IActionResult AppendEntry(AppendEntryRequest request)
    {
        node.AppendEntry(request);
        return Ok();
    }

    [HttpPost("request-vote")]
    public ActionResult<VoteResponse> RequestVote(VoteRequest request)
    {
        var votedYes = node.VoteForCandidate(request); 
        return new VoteResponse
        {
            VoterId = node.Id,
            VoteGranted = votedYes
        };
    }
}
