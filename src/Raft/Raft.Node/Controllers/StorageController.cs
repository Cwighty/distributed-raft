using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Raft.Data.Models;

namespace Raft.Node.Controllers;

[ApiController]
[Route("[controller]")]
public class StorageController : ControllerBase
{
    private readonly ILogger<StorageController> _logger;
    private readonly NodeService node;

    public StorageController(ILogger<StorageController> logger, NodeService node)
    {
        _logger = logger;
        this.node = node;
    }

    [HttpGet("StrongGet")]
    public async Task<ActionResult<VersionedValue<string>>> StrongGet([FromQuery] string key)
    {
        _logger.LogInformation("StrongGet called with key: {key}", key);
        if (node.IsLeader)
        {
            return await node.StrongGet(key);
        }
        else
        {
            throw new Exception("Not the leader.");
        }
    }

    [HttpGet("EventualGet")]
    public VersionedValue<string> EventualGet([FromQuery] string key)
    {
        _logger.LogInformation("EventualGet called with key: {key}", key);
        return node.EventualGet(key);
    }

    [HttpPost("CompareAndSwap")]
    public async Task<ActionResult> CompareAndSwap(CompareAndSwapRequest request)
    {
        _logger.LogInformation("CompareAndSwap called with key: {key}, oldValue: {value}, newValue: {newValue}", request.Key, request.OldValue, request.NewValue);
        await node.CompareAndSwap(request.Key, request.OldValue, request.NewValue);
        return Ok();
    }

}
