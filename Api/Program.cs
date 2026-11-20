using Api;
using Microsoft.Azure.Functions.Worker.Builder;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.Services.Configure<DeployTriggerOptions>(builder.Configuration.GetSection(DeployTriggerOptions.Section));

builder.Services.AddHttpClient<DeployTrigger>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
