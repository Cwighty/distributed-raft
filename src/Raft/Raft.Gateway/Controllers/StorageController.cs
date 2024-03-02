using Microsoft.AspNetCore.Mvc;
using Raft.Data.Models;

namespace Raft.Gateway.Controllers;

[ApiController]
[Route("[controller]")]
public class StorageController : ControllerBase
{
    private readonly ILogger<StorageController> _logger;

    public StorageController(ILogger<StorageController> logger)
    {
        _logger = logger;
    }

    [HttpGet("StrongGet")]
    public VersionedValue<string> StrongGet([FromQuery] string key)
    {
        _logger.LogInformation("StrongGet called with key: {key}", key);
        // var leader = FindLeader();
        // return leader.StrongGet(key);
        return new VersionedValue<string> { Version = 1, Value = "value" };
    }

    [HttpGet(Name = "EventualGet")]
    public VersionedValue<string> EventualGet([FromQuery] string key)
    {
        _logger.LogInformation("EventualGet called with key: {key}", key);
        // var node = GetRandomNode();
        // return node.EventualGet(key);
        return new VersionedValue<string> { Version = 1, Value = "value" };
    }

    [HttpPost(Name = "CompareAndSwap")]
    public void CompareAndSwap(CompareAndSwapRequest request)
    {
        _logger.LogInformation("CompareAndSwap called with key: {key}, oldValue: {oldValue}, newValue: {newValue}", request.Key, request.OldValue, request.NewValue);
        // var leader = FindLeader();
        // leader.CompareAndSwap(key, oldValue, newValue);
    }

    // private Node FindLeader()
    // {
    //     var node = GetRandomNode();

    //     if (node.IsLeader)
    //     {
    //         return node;
    //     }
    //     else
    //     {
    //         if (node.LeaderId == Guid.Empty)
    //         {
    //             while (node.LeaderId == Guid.Empty) // wait for leader to be elected
    //             {
    //                 node = GetRandomNode();
    //             }
    //         }
    //         return nodeDict[node.LeaderId];
    //     }
    // }

    // private Node GetRandomNode()
    //     {
    //         var random = new Random();
    //         return nodeDict.ElementAt(random.Next(nodeDict.Count)).Value;
    //     }
}
