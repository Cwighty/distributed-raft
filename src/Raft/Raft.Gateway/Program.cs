using Raft.Gateway.Options;
using Raft.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddObservability();
builder.AddApiOptions();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
        {
            c.RouteTemplate = "gateway/swagger/{documentname}/swagger.json";
        });


    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/gateway/swagger/v1/swagger.json", "My Cool API V1");
        c.RoutePrefix = "gateway/swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
