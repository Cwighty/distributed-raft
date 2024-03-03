namespace Raft.Data.Models;

public class VoteResponse
{
    public Guid VoterId { get; set; }
    public bool VoteGranted { get; set; }
}