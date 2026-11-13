using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Client.Interop;

/// <summary>
/// A collocated JavaScript module, imported once on first use and released on dispose.
/// Callers that start at the same time share the one import.
/// </summary>
internal sealed class JsModule(IJSRuntime js, string path) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _import;

    /// <summary>
    /// Returns the module, importing it on the first call. After <see cref="DisposeAsync"/> this still returns the released
    /// module; calls on it then fail with a <see cref="JSException"/>, which the interop classes treat as "page already left".
    /// </summary>
    public async Task<IJSObjectReference> GetAsync()
    {
        Task<IJSObjectReference>? attempt = null;

        try
        {
            attempt = _import ??= js.InvokeAsync<IJSObjectReference>("import", path).AsTask();
            return await attempt;
        }
        catch (JSException)
        {
            // A failed import (a network hiccup, say) is retried on the next call instead of failing forever.
            // Only this failed attempt is forgotten: another caller may already have started a newer import.
            if (_import == attempt)
            {
                _import = null;
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_import is not { } import)
        {
            return;
        }

        try
        {
            await (await import).DisposeAsync();
        }
        catch (JSException)
        {
            // Never imported successfully, or already gone: nothing to release.
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Stops a running effect and releases its handle. Each step runs even if the other fails,
    /// so a failing stop can't leave the handle behind.
    /// </summary>
    public static async ValueTask StopAndReleaseAsync(IJSObjectReference handle, string stopMethod, ILogger logger, string effect)
    {
        try
        {
            await handle.InvokeVoidAsync(stopMethod);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "The {Effect} could not be stopped.", effect);
        }

        try
        {
            await handle.DisposeAsync();
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
