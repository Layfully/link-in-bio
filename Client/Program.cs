using Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Storyblok;
using Storyblok.Extensions;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStoryblok(builder.Configuration.GetSection("Storyblok"), typeof(App).Assembly);
builder.Services.AddHostedService<PrerenderedStoryLoader>();

// Drafts require a preview token and a Visual Editor URL.
builder.Services.AddOptions<StoryblokOptions>()
    .Configure<NavigationManager>((options, navigation) =>
        options.IsInEditor = !string.IsNullOrWhiteSpace(options.ApiKeyPreview)
            && VisualEditor.HasOpened(new Uri(navigation.Uri)));

await builder.Build().RunAsync();
