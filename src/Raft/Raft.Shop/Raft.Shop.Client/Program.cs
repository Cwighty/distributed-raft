using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Raft.Shop.Client;
public class Program
{
    public static async global::System.Threading.Tasks.Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        await builder.Build().RunAsync();
    }
}