namespace Raft.Gateway.Options;

public class ApiOptions
{
    public string NodeServiceName { get; set; } = "node";
    public int NodeServicePort { get; set; } = 8080;
    public int NodeCount { get; set; } = 3;
}

public static class ApiOptionsExtensions
{
    public static WebApplicationBuilder AddApiOptions(this WebApplicationBuilder builder)
    {
        var apiOptions = new ApiOptions();
        builder.Configuration.Bind(nameof(ApiOptions), apiOptions);
        builder.Services.AddSingleton(apiOptions);
        return builder;
    }
}
