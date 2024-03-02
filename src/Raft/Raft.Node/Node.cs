namespace Raft.Node;

public class Node
{
    private readonly HttpClient client;
    private readonly ILogger<Node> logger;

    public Guid Id { get; private set; }

    public Node(HttpClient client, ILogger<Node> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    public (string value, int version) Get(string key)
    {
        return ("value", 1);
    }
}