using Callora.Administration;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Surface.Rendering;
using Callora.Workspace;

// The repository's own runnable composition — what `docker-compose up` and an F5 start.
//
// This repository is a framework: the modules below are packable libraries and the shipping host
// lives in callora-production. That split is right, and it left a gap: nothing here could be run.
// The compose file still pointed at Callora.Core, which has been OutputType=Library since the module
// split, so the dev stack could not start at all and the release workflow published a "host" with no
// entry point.
//
// This host closes that gap and nothing more. It is deliberately the same handful of calls the
// distribution host makes, because that is its second job: if someone breaks AddCalloraHost or the
// order these have to run in, this stops building rather than the next distribution finding out.
//
// It is not a second product. No installer, no first-run provisioning, no packaging — those belong
// to a distribution, which owns its own configuration and lifecycle.
var builder = WebApplication.CreateBuilder(args);
builder.AddCalloraHost();
builder.AddCalloraAdministration();
builder.Services.AddCalloraSurfaceRendering();

var app = builder.Build();
app.MapCalloraHost();
app.MapCalloraAdministration();
app.MapCalloraSurfaceRendering();
app.MapCalloraWorkspace();

await app.RunAsync();
