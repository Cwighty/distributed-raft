namespace Raft.Data.Models;
public class VoteRequest
{
    public int CandidateId { get; set; }
    public int Term { get; set; }
    public long LastLogIndex { get; set; }
}