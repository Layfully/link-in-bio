// Google Analytics runs only after the visitor allows it. Until then no Google script loads, no cookies are set and nothing is sent.
// The answer is remembered in this browser for 13 months, then asked again; "Analytics settings" withdraws it at any time.
(() => {
    const MEASUREMENT_ID = 'G-BCKR10P1MJ';
    const STORAGE_KEY = 'analytics-consent';

    // European regulators (the French CNIL most explicitly) expect consent and analytics cookies to last at most 13 months.
    const CONSENT_LIFETIME_MS = 395 * 24 * 60 * 60 * 1000;
    const COOKIE_LIFETIME_SECONDS = 395 * 24 * 60 * 60;

    const banner = document.getElementById('analytics-consent');
    const settings = document.getElementById('analytics-settings');
    let analyticsLoaded = false;

    // Stored as { choice, at }. Anything else, including an answer older than 13 months, counts as not asked yet.
    function parseChoice(stored) {
        try {
            const { choice, at } = JSON.parse(stored);
            const known = choice === 'granted' || choice === 'denied';
            return known && Date.now() - at < CONSENT_LIFETIME_MS ? choice : null;
        } catch {
            return null;
        }
    }

    const readChoice = () => { try { return parseChoice(localStorage.getItem(STORAGE_KEY)); } catch { return null; } };
    const saveChoice = choice => {
        try {
            if (choice) localStorage.setItem(STORAGE_KEY, JSON.stringify({ choice, at: Date.now() }));
            else localStorage.removeItem(STORAGE_KEY);
        } catch { }
    };

    function loadAnalytics() {
        // An earlier "No thanks" in this tab set Google's opt-out flag; a new "Allow" lifts it.
        window[`ga-disable-${MEASUREMENT_ID}`] = false;

        if (analyticsLoaded) return;
        analyticsLoaded = true;

        window.dataLayer = window.dataLayer || [];
        // gtag expects the arguments object itself, not an array.
        window.gtag = function () { window.dataLayer.push(arguments); };
        window.gtag('js', new Date());
        window.gtag('config', MEASUREMENT_ID, { cookie_expires: COOKIE_LIFETIME_SECONDS });

        const script = document.createElement('script');
        script.async = true;
        script.src = `https://www.googletagmanager.com/gtag/js?id=${MEASUREMENT_ID}`;
        document.head.appendChild(script);
    }

    function removeAnalyticsCookies() {
        // Analytics may have set its cookies on this host or a parent domain, but never on a bare top-level domain like "dev".
        const parts = window.location.hostname.split('.');
        const domains = parts.slice(0, -1).map((_, i) => parts.slice(i).join('.'));

        for (const cookie of document.cookie.split(';')) {
            const name = cookie.split('=')[0].trim();
            if (!name.startsWith('_ga')) continue;

            for (const domain of domains) {
                document.cookie = `${name}=; Max-Age=0; path=/; domain=${domain}`;
            }
            document.cookie = `${name}=; Max-Age=0; path=/`;
        }
    }

    // Without consent nothing may run: stop a running Google script first (Google's own opt-out flag), so it can't write
    // its cookies back, then remove them. A running script can't be unloaded, so only a tab where Analytics already ran
    // reloads, to start clean.
    function withdraw() {
        window[`ga-disable-${MEASUREMENT_ID}`] = true;
        removeAnalyticsCookies();
        if (analyticsLoaded) window.location.reload();
    }

    // Applies a choice to this tab: "granted" loads Analytics, "denied" or no answer withdraws it.
    // The banner is shown only while there is no answer.
    function apply(choice) {
        if (choice === 'granted') loadAnalytics();
        else withdraw();
        banner.hidden = choice !== null;
    }

    banner.addEventListener('click', event => {
        const answer = event.target.closest('[data-consent]')?.dataset.consent;
        if (!answer) return;

        saveChoice(answer);
        apply(answer);
    });

    // Asks again without changing anything: the stored answer stays until the visitor picks one of the buttons.
    // Only switching from "Allow" to "No thanks" reloads the page.
    settings.addEventListener('click', () => {
        banner.hidden = false;
        banner.querySelector('[data-consent]')?.focus();
    });

    // An answer in another tab of this site, or its expiry, takes effect here too.
    window.addEventListener('storage', event => {
        if (event.key !== STORAGE_KEY) return;

        apply(parseChoice(event.newValue));
    });

    const choice = readChoice();
    if (choice === null) saveChoice(null); // drop an expired or unreadable answer
    apply(choice);
})();
