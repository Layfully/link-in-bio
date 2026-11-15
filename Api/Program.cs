using Api;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.Services.Configure<DeployTriggerOptions>(builder.Configuration.GetSection(DeployTriggerOptions.Section));

// One client for every call: it sends one request per publish, to one host. 10 seconds per attempt, three attempts and
// the pauses between them (33 seconds at most) stay inside the 45 seconds Static Web Apps gives a managed function's
// request, the tighter limit; Storyblok waits 120 seconds for a webhook's answer.
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DeployTrigger>();

await builder.Build().RunAsync();
