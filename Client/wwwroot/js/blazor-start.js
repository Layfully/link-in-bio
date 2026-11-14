// Starts Blazor, which index.html loads with autostart="false".
//
// Starting .NET in the browser takes about a second of the main thread's time. The deployed home page doesn't need it
// to show anything: its content is in the HTML already (see the README, Prerendering). So there it starts when the
// visitor first does something (moves the pointer, taps, scrolls, types, tabs) or 5 seconds after the page has loaded,
// whichever comes first; the page stays responsive for that first look. Every other page (certificates, unknown
// addresses, the editor build, local development) has nothing to show without Blazor and starts it at once.
// Links are plain links, so a click before Blazor runs is an ordinary page load and works the same.
(() => {
    const FALLBACK_DELAY_MS = 5000;
    const INTERACTIONS = ['pointermove', 'pointerdown', 'touchstart', 'wheel', 'scroll', 'keydown', 'focusin'];

    let started = false;

    function start() {
        if (started) return;
        started = true;

        for (const type of INTERACTIONS) {
            removeEventListener(type, start, { capture: true });
        }

        Blazor.start();
    }

    function decide() {
        // Written into the page by the deploy's prerender step, together with the content it shows.
        if (document.getElementById('storyblok-prerendered') === null) {
            start();
            return;
        }

        for (const type of INTERACTIONS) {
            addEventListener(type, start, { capture: true, passive: true });
        }

        addEventListener('load', () => setTimeout(start, FALLBACK_DELAY_MS));
    }

    // The prerendered data comes at the end of the body, after this script, so the question waits until the page has
    // been parsed. For every other page that is a moment later and changes nothing.
    if (document.readyState === 'loading') {
        addEventListener('DOMContentLoaded', decide);
    } else {
        decide();
    }
})();
