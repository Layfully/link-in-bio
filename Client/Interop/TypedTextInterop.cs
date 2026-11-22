using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Client.Interop;

/// <summary>Runs one typing animation at a time and logs JavaScript failures.</summary>
public sealed class TypedTextInterop(IJSRuntime js, ILogger logger) : IAsyncDisposable
{
    internal const string ModulePath = "./Components/TypedText.razor.js";
    internal const string StartMethod = "start";
    internal const string StopMethod = "stop";

    private readonly JsModule _module = new(js, ModulePath);
    private IJSObjectReference? _animation;
    private bool _disposed;

    // Each start gets a number; an older start that finishes after a newer one began is discarded.
    private int _latestStart;

    public async ValueTask StartAsync(ElementReference element, IReadOnlyList<string> texts)
    {
        int start = ++_latestStart;
        await StopRunningAsync();

        try
        {
            IJSObjectReference module = await _module.GetAsync();
            if (IsSuperseded(start))
            {
                return;
            }

            // Null when the element was already gone by the time the module ran.
            IJSObjectReference? animation = await module.InvokeAsync<IJSObjectReference?>(StartMethod, element, texts);

            if (!IsSuperseded(start))
            {
                _animation = animation;
            }
            else if (animation is not null)
            {
                await JsModule.StopAndReleaseAsync(animation, StopMethod, logger, "typing animation");
            }
        }
        catch (JSException exception)
        {
            if (!IsSuperseded(start))
            {
                logger.LogWarning(exception, "The typing animation could not start.");
            }
        }
    }

    /// <summary>Ends the running animation and discards a start that is still in progress.</summary>
    public ValueTask StopAsync()
    {
        _latestStart++;
        return StopRunningAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await StopRunningAsync();
        await _module.DisposeAsync();
    }

    private bool IsSuperseded(int start) => _disposed || start != _latestStart;

    private ValueTask StopRunningAsync()
    {
        IJSObjectReference? animation = _animation;
        _animation = null;
        return animation is null
            ? ValueTask.CompletedTask
            : JsModule.StopAndReleaseAsync(animation, StopMethod, logger, "typing animation");
    }
}
