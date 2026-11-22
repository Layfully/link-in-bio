using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Storyblok;

namespace Client;

/// <summary>Seeds the story cache before Blazor renders its root components.</summary>
internal sealed class PrerenderedStoryLoader(
    IJSRuntime jsRuntime,
    PrerenderedStories stories,
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
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "The prerendered content can't be read.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
