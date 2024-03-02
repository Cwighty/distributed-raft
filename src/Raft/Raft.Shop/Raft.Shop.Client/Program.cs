using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Raft.Shop.Client;
public class Program
{
    public static async global::System.Threading.Tasks.Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.Services.AddHttpClient("ApiClient", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

        await builder.Build().RunAsync();
    }
}