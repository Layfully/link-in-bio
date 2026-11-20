using Api;
using Microsoft.Azure.Functions.Worker.Builder;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.Services.Configure<DeployTriggerOptions>(builder.Configuration.GetSection(DeployTriggerOptions.Section));

builder.Services.AddDeployTriggerClient();

await builder.Build().RunAsync();
