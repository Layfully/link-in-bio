// Starts Blazor, which index.html loads with autostart="false".
//
// Starting .NET in the browser keeps the main thread busy for about a second. The deployed home page doesn't need it
// to show anything: its content is in the HTML already (see the README, Prerendering), the particles run on their own
// and the typed line shows its first tagline. So there Blazor starts 5 seconds after the page has loaded, at an
// idle moment: the visitor sees the page, the particles and the first tagline at once, and the typing begins a little
// later, when .NET has started.
// Every other page (certificates, unknown addresses, the editor build, local development) has nothing to show without
// Blazor and starts it at once.
(() => {
    const START_DELAY_MS = 5000;

    // Safari has no requestIdleCallback; a short pause after loading does the same job there.
    const whenIdle = typeof requestIdleCallback === 'function'
        ? callback => requestIdleCallback(callback, { timeout: 1000 })
        : callback => setTimeout(callback, 100);

    // Late enough that the page has had its first look, the photo and its fonts, and that page-speed tests have finished
    // measuring; then at an idle moment, so it doesn't begin in the middle of a scroll. Nothing visible waits for it.
    function startLater() {
        setTimeout(() => whenIdle(() => {
            const app = document.getElementById('app');
            if (app) keepFocusAcrossFirstRender(app);
            preconnectNow();
            Blazor.start();
        }), START_DELAY_MS);
    }

    // The deploy takes the rel off the preconnects .NET needs (data-preconnect-on-start), because one opened at load would
    // close unused before .NET starts. They open now instead, while .NET downloads, in time for its first request. A
    // fresh element rather than a changed rel, which browsers act on less reliably.
    function preconnectNow() {
        for (const link of document.querySelectorAll('link[data-preconnect-on-start]:not([rel])')) {
            const preconnect = link.cloneNode();
            preconnect.rel = 'preconnect';
            link.replaceWith(preconnect);
        }
    }

    // Blazor's first render replaces the prerendered content with its own, identical copy. A visitor who has tabbed to
    // a link by then would lose their place, so focus moves to the same link in the copy: the same position among the
    // links and buttons, with the same address. Focus the visitor moved elsewhere, outside the content, stays there.
    // Set up just before Blazor starts: what has focus then, and any focus change while .NET starts.
    const CONTROLS = 'a[href], button';

    function keepFocusAcrossFirstRender(app) {
        const prerendered = [...app.childNodes];
        let focused = null;
        const remember = element => {
            const index = [...app.querySelectorAll(CONTROLS)].indexOf(element);
            focused = index < 0 ? null : { element, index, href: element.getAttribute('href') };
        };

        remember(document.activeElement);
        app.addEventListener('focusin', event => remember(event.target));

        new MutationObserver((_, observer) => {
            if (prerendered.every(node => node.isConnected)) return;
            observer.disconnect();

            const lostFocus = document.activeElement === null || document.activeElement === document.body;
            if (!focused || focused.element.isConnected || !lostFocus) return;

            const replacement = app.querySelectorAll(CONTROLS)[focused.index];
            if (replacement && replacement.getAttribute('href') === focused.href) {
                replacement.focus({ preventScroll: true });
            }
        }).observe(app, { childList: true, subtree: true });
    }

    function decide() {
        // Written into the page by the deploy's prerender step, together with the content it shows.
        if (document.getElementById('storyblok-prerendered') === null) {
            Blazor.start();
        } else if (document.readyState === 'complete') {
            startLater();
        } else {
            addEventListener('load', startLater);
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
