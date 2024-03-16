using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Raft.Node;
using Raft.Node.Options;
using Raft.Node.Services;
using Raft.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddObservability(logToConsole: false);
builder.AddApiOptions();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<INodeClient, NodeClient>();

builder.Services.AddHttpClient();
if (builder.Environment.IsProduction())
{
    // remove all http client request logging
    builder.Services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
}
builder.Services.AddSingleton<NodeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<NodeService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
