namespace Raft.Data.Models;
public class VoteRequest
{
    public Guid CandidateId { get; set; }
    public int Term { get; set; }
    public long LastLogIndex { get; set; }
    public int LastLogTerm { get; set; }
}