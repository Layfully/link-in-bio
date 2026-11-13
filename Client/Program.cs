using Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Storyblok;
using Storyblok.Extensions;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddStoryblok(builder.Configuration.GetSection("Storyblok"));

// Only inside the Visual Editor are drafts loaded with the preview token.
builder.Services.AddOptions<StoryblokOptions>()
    .Configure<NavigationManager>((options, navigation) => options.IsInEditor = IsOpenedByVisualEditor(new Uri(navigation.Uri)));

await builder.Build().RunAsync();

static bool IsOpenedByVisualEditor(Uri address) =>
    address.Query.TrimStart('?').Split('&').Any(parameter => parameter == "_storyblok" || parameter.StartsWith("_storyblok=", StringComparison.Ordinal));
