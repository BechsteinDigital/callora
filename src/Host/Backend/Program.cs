using Callora.Host.Backend.Infrastructure.DependencyInjection;

// Thin composition root. All wiring lives in the framework
// (CalloraHostCompositionExtensions); the distribution skeleton owns only this.
var builder = WebApplication.CreateBuilder(args);
builder.AddCalloraHost();

var app = builder.Build();
app.MapCalloraHost();

await app.RunAsync();
