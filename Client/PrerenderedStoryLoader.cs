using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Storyblok;

namespace Client;

/// <summary>Loads the prerendered story and canonical origin before Blazor renders.</summary>
internal sealed class PrerenderedStoryLoader(
    IJSRuntime jsRuntime,
    PrerenderedStories stories,
    Site site,
    ILogger<PrerenderedStories> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            IJSInProcessRuntime js = (IJSInProcessRuntime)jsRuntime;
            using IJSInProcessObjectReference? element = js.Invoke<IJSInProcessObjectReference?>(
                "document.getElementById", PrerenderedStories.ElementId);
            if (element?.GetValue<string?>("textContent") is { } prerendered)
            {
                stories.AddAll(prerendered);
            }

            using IJSInProcessObjectReference? metadata = js.Invoke<IJSInProcessObjectReference?>(
                "document.querySelector", "meta[property='og:url']");
            site.Url = Site.ParseOrigin(metadata?.GetValue<string?>("content"));
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "The prerendered content can't be read.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
