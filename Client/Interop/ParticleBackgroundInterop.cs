using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Client.Interop;

/// <summary>
/// Owns the JavaScript module behind the <c>ParticleBackground</c> component.
/// A failing start, for example because the CDN script didn't load, is logged and never breaks the page.
/// </summary>
public sealed class ParticleBackgroundInterop(IJSRuntime js, ILogger logger) : IAsyncDisposable
{
    internal const string ModulePath = "./Components/ParticleBackground.razor.js";
    internal const string StartParticlesMethod = "startParticles";
    internal const string StopMethod = "stop";

    private readonly JsModule _module = new(js, ModulePath);
    private IJSObjectReference? _particles;
    private bool _disposed;

    public async ValueTask StartParticlesAsync(ElementReference container)
    {
        try
        {
            IJSObjectReference module = await _module.GetAsync();
            if (_disposed)
            {
                return;
            }

            // Null when the page was already gone by the time the module ran.
            IJSObjectReference? particles = await module.InvokeAsync<IJSObjectReference?>(StartParticlesMethod, container);

            // The page may have been left while the particles were starting; nothing else would ever stop them.
            if (_disposed)
            {
                if (particles is not null)
                {
                    await JsModule.StopAndReleaseAsync(particles, StopMethod, logger, "particle background");
                }

                return;
            }

            _particles = particles;
        }
        catch (JSException exception) when (!_disposed)
        {
            logger.LogWarning(exception, "The particle background could not start.");
        }
        catch (JSException)
        {
            // Failed only because the page was left mid-start: not worth a warning.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_particles is { } particles)
        {
            _particles = null;
            await JsModule.StopAndReleaseAsync(particles, StopMethod, logger, "particle background");
        }

        await _module.DisposeAsync();
    }
}
