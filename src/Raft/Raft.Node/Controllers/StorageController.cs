using Microsoft.AspNetCore.Mvc;
using Raft.Data.Models;

namespace Raft.Node.Controllers;

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
    public (string value, int version) StrongGet([FromQuery] string key)
    {
        _logger.LogInformation("StrongGet called with key: {key}", key);

        // var confirmLeaderCount = 1;
        // foreach (var node in allNodes)
        // {
        //     if (node.Id == Id)
        //     {
        //         continue;
        //     }
        //     if (node.ConfirmLeader(Id))
        //     {
        //         confirmLeaderCount++;
        //     }
        //     if (confirmLeaderCount > allNodes.Count / 2)
        //     {
        //         if (data.ContainsKey(key))
        //             return data[key];
        //         else
        //             return (null, 0);
        //     }
        // }

        // throw new Exception("Leader could not be confirmed.");
        return ("value", 1);
    }

    [HttpGet("EventualGet")]
    public (string value, int version) EventualGet([FromQuery] string key)
    {
        _logger.LogInformation("EventualGet called with key: {key}", key);
        // if (data.ContainsKey(key))
        // {
        //     return data[key];
        // }

        // return (null, 0);
        return ("value", 1);
    }

    [HttpPost("CompareAndSwap")]
    public void CompareAndSwap(CompareAndSwapRequest request)
    {
        // if (!IsLeader)
        // {
        //     throw new Exception("Not the leader.");
        // }

        // var newIndex = 1;
        // if (Directory.Exists(entryLogPath))
        // {
        //     newIndex = entryLogDirectory.GetFiles().Length + 1;
        // }
        // if (data.ContainsKey(key) && oldValue != data[key].value)
        //     throw new Exception("Value does not match.");

        // LogEntry(key, newValue, newIndex, CurrentTerm);
        // if (BroadcastReplication(key, newValue, newIndex))
        // {
        //     // if majority of nodes have replicated the log, update the data
        //     data[key] = (newValue, newIndex);
        //     CommittedIndex = newIndex;
        //     return;
        // }
        // throw new Exception("Could not replicate to majority of nodes.");
    }

}
