using Client;
using Client.Data.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Storyblok;
using Storyblok.Extensions;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The clock behind the loading hints (StoryblokStatus); tests replace it to move time forward.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddStoryblok(builder.Configuration.GetSection("Storyblok"), ComponentJson.Default, typeof(App).Assembly);

// Drafts are loaded only inside the Visual Editor, and only by the editor build, the one deployment that has the preview
// token. Production ships without it, so "?_storyblok" there shows the published content like any other visit.
builder.Services.AddOptions<StoryblokOptions>()
    .Configure<NavigationManager>((options, navigation) =>
        options.IsInEditor = !string.IsNullOrWhiteSpace(options.ApiKeyPreview) && VisualEditor.HasOpened(new Uri(navigation.Uri)));

WebAssemblyHost host = builder.Build();

// The deployed home page carries Storyblok's response for its story (written by the Prerender project), so the app's
// first render can show the same content the HTML already does instead of a placeholder. Read before the first render.
// Without it, or if it can't be read, the page simply loads its content from Storyblok.
try
{
    IJSInProcessRuntime js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
    using IJSInProcessObjectReference? element = js.Invoke<IJSInProcessObjectReference?>("document.getElementById", PrerenderedStories.ElementId);
    if (element?.GetValue<string?>("textContent") is { } prerendered)
    {
        host.Services.GetRequiredService<PrerenderedStories>().AddAll(prerendered);
    }
}
catch (JSException exception)
{
    host.Services.GetRequiredService<ILogger<PrerenderedStories>>().LogWarning(exception, "The prerendered content can't be read.");
}

await host.RunAsync();
