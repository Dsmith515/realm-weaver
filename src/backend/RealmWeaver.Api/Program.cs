using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.OpenApi;

using RealmWeaver.Api;
using RealmWeaver.Infrastructure.Http;
using RealmWeaver.Infrastructure.Persistence;
using RealmWeaver.Modules.Catalog;
using RealmWeaver.Modules.Inventory;
using RealmWeaver.Modules.Realms;
using RealmWeaver.Modules.Rituals;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
{
    // Seeds are 64-bit and always sent as strings (JavaScript numbers can't hold them exactly).
    if (context.JsonTypeInfo.Type == typeof(ulong))
    {
        schema.Type = JsonSchemaType.String;
        schema.Pattern = "^[0-9]+$";
    }

    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
          .AllowAnyHeader()
          .AllowAnyMethod()));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    // Numbers are numbers. (The web default also accepts "12", which makes every integer `number | string` in TypeScript.)
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddSingleton(TimeProvider.System);

// Malformed input is always a 400 ProblemDetails, also in Development (where the default is to throw).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// Modules. Each registers its own DbContext (own schema), services, contract and database initializer.
builder.Services.AddCatalogModule();
builder.Services.AddInventoryModule();
builder.Services.AddRealmsModule();
builder.Services.AddRitualsModule();

var app = builder.Build();

// Local-first app: migrate and seed on startup. A release pipeline would run an EF migration bundle instead.
if (app.Configuration.GetValue("Database:InitializeOnStartup", defaultValue: true))
{
    await app.Services.InitializeModuleDatabasesAsync();
}

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapGet("/api/info", () => new ApiInfo("Realm Weaver", typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
   .WithName("GetApiInfo");

app.MapCatalogModule();
app.MapInventoryModule();
app.MapRealmsModule();
app.MapRitualsModule();
app.MapMapDetails();

await app.RunAsync();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
