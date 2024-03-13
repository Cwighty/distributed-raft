namespace Raft.Data.Models;

public class VoteResponse
{
    public int VoterId { get; set; }
    public bool VoteGranted { get; set; }
}
