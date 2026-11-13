using Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Storyblok;
using Storyblok.Extensions;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddStoryblok(builder.Configuration.GetSection("Storyblok"), typeof(App).Assembly);

// Drafts are loaded only inside the Visual Editor, and only by the editor build, the one deployment that has the preview
// token. Production ships without it, so "?_storyblok" there shows the published content like any other visit.
builder.Services.AddOptions<StoryblokOptions>()
    .Configure<NavigationManager>((options, navigation) =>
        options.IsInEditor = !string.IsNullOrWhiteSpace(options.ApiKeyPreview) && IsOpenedByVisualEditor(new Uri(navigation.Uri)));

await builder.Build().RunAsync();

static bool IsOpenedByVisualEditor(Uri address) =>
    address.Query.TrimStart('?').Split('&').Any(parameter => parameter == "_storyblok" || parameter.StartsWith("_storyblok=", StringComparison.Ordinal));
