// Start Blazor immediately unless the home page already has prerendered content.
(() => {
    const START_DELAY_MS = 5000;
    const CONTROLS = 'a[href], button';

    // Safari has no requestIdleCallback; a short pause after loading does the same job there.
    const whenIdle = typeof requestIdleCallback === 'function'
        ? callback => requestIdleCallback(callback, { timeout: 1000 })
        : callback => setTimeout(callback, 100);

    function startLater() {
        setTimeout(() => whenIdle(() => {
            const app = document.getElementById('app');
            if (app) keepFocusAcrossFirstRender(app);
            preconnectNow();
            Blazor.start();
        }), START_DELAY_MS);
    }

    // Insert fresh preconnect links so browsers open them when Blazor starts.
    function preconnectNow() {
        for (const link of document.querySelectorAll('link[data-preconnect-on-start]:not([rel])')) {
            const preconnect = link.cloneNode();
            preconnect.rel = 'preconnect';
            link.replaceWith(preconnect);
        }
    }

    // Restore focus only if Blazor replaced the focused control and focus was lost.
    function keepFocusAcrossFirstRender(app) {
        const prerendered = [...app.childNodes];
        let focused = null;
        const remember = element => {
            const index = [...app.querySelectorAll(CONTROLS)].indexOf(element);
            focused = index < 0 ? null : { element, index, href: element.getAttribute('href') };
        };

        remember(document.activeElement);
        const rememberFocus = event => remember(event.target);
        app.addEventListener('focusin', rememberFocus);

        new MutationObserver((_, observer) => {
            if (prerendered.every(node => node.isConnected)) return;
            observer.disconnect();
            app.removeEventListener('focusin', rememberFocus);

            const lostFocus = document.activeElement === null || document.activeElement === document.body;
            if (!focused || focused.element.isConnected || !lostFocus) return;

            const replacement = app.querySelectorAll(CONTROLS)[focused.index];
            if (replacement && replacement.getAttribute('href') === focused.href) {
                replacement.focus({ preventScroll: true });
            }
        }).observe(app, { childList: true, subtree: true });
    }

    function start() {
        if (!document.getElementById('storyblok-prerendered')) {
            Blazor.start();
            return;
        }

        if (document.readyState === 'complete') {
            startLater();
        } else {
            addEventListener('load', startLater, { once: true });
        }
    }

    // The embedded story data appears after this script in the published HTML.
    if (document.readyState === 'loading') {
        addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
