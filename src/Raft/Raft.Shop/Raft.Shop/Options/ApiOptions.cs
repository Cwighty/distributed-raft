namespace Raft.Shop.Options;

public class ApiOptions
{
    public string GatewayAddress { get; set; } = null!;

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
