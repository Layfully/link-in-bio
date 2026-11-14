// Starts Blazor, which index.html loads with autostart="false".
//
// Starting .NET in the browser keeps the main thread busy for about a second. The deployed home page doesn't need it
// to show anything: its content is in the HTML already (see the README, Prerendering), the particles run on their own
// and the typed line shows its first tagline. So there Blazor starts only once the page has loaded and the browser is
// idle: the first paint, the photo and the fonts come first, then .NET, before most visitors have done anything.
// Every other page (certificates, unknown addresses, the editor build, local development) has nothing to show without
// Blazor and starts it at once.
(() => {
    // Safari has no requestIdleCallback; a short pause after loading does the same job there.
    const whenIdle = typeof requestIdleCallback === 'function'
        ? callback => requestIdleCallback(callback, { timeout: 1000 })
        : callback => setTimeout(callback, 100);

    function decide() {
        // Written into the page by the deploy's prerender step, together with the content it shows.
        if (document.getElementById('storyblok-prerendered') === null) {
            Blazor.start();
        } else if (document.readyState === 'complete') {
            whenIdle(() => Blazor.start());
        } else {
            addEventListener('load', () => whenIdle(() => Blazor.start()));
        }
    }

    // The prerendered data comes at the end of the body, after this script, so the question waits until the page has
    // been parsed. For every other page that is a moment later and changes nothing.
    if (document.readyState === 'loading') {
        addEventListener('DOMContentLoaded', decide);
    } else {
        decide();
    }
})();
