using System.Globalization;
using Raft.Data.Services;
using Raft.Observability;
using Raft.Shop.Client.Pages;
using Raft.Shop.Components;
using Raft.Shop.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.AddApiOptions();
builder.AddObservability();

builder.Services.AddHttpClient("GatewayClient", client => client.BaseAddress = new Uri(builder.Configuration.GetSection(nameof(ApiOptions))["GatewayAddress"] ?? throw new InvalidOperationException("Gateway address not found.")));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("GatewayClient"));

builder.Services.AddScoped<IAccountService, AccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

app.Run();
