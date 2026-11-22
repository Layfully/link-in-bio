using Microsoft.JSInterop;

namespace Client.Interop;

/// <summary>Shares a lazy JavaScript module import among callers and releases it on disposal.</summary>
internal sealed class JsModule(IJSRuntime js, string path) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _import;

    /// <summary>
    /// Returns the shared module. After disposal, the cached reference is already released.
    /// </summary>
    public async Task<IJSObjectReference> GetAsync()
    {
        Task<IJSObjectReference>? attempt = null;

        try
        {
            attempt = _import ??= js.InvokeAsync<IJSObjectReference>("import", path).AsTask();
            return await attempt;
        }
        catch (JSException) when (_import == attempt)
        {
            // Let the next call retry while preserving any newer attempt.
            _import = null;
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
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            // Never imported successfully, or already gone: nothing to release.
        }
    }

    /// <summary>Stops an effect and releases its handle even if JavaScript fails to stop it.</summary>
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
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
        }
    }
}
