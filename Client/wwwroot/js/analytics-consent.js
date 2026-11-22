// Load Google Analytics only after consent on the public site.
(() => {
    const MEASUREMENT_ID = 'G-BCKR10P1MJ';
    const STORAGE_KEY = 'analytics-consent';

    const LIFETIME_SECONDS = 395 * 24 * 60 * 60;

    const PRODUCTION_HOSTS = ['adriangaborek.dev', 'www.adriangaborek.dev'];

    const banner = document.getElementById('analytics-consent');
    const settings = document.getElementById('analytics-settings');
    let analyticsLoaded = false;

    if (!PRODUCTION_HOSTS.includes(window.location.hostname)) {
        settings.hidden = true;
        return;
    }

    function parseChoice(stored) {
        try {
            const { choice, at } = JSON.parse(stored);
            const known = choice === 'granted' || choice === 'denied';
            return known && Date.now() - at < LIFETIME_SECONDS * 1000 ? choice : null;
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
        window[`ga-disable-${MEASUREMENT_ID}`] = false;

        if (analyticsLoaded) return;
        analyticsLoaded = true;

        window.dataLayer = window.dataLayer || [];
        // gtag expects the arguments object itself, not an array.
        window.gtag = function () { window.dataLayer.push(arguments); };
        window.gtag('js', new Date());
        window.gtag('config', MEASUREMENT_ID, { cookie_expires: LIFETIME_SECONDS });

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

    // Disable Analytics before clearing cookies and reloading a tab that already loaded it.
    function withdraw() {
        window[`ga-disable-${MEASUREMENT_ID}`] = true;
        removeAnalyticsCookies();
        if (analyticsLoaded) window.location.reload();
    }

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

    settings.addEventListener('click', () => {
        banner.hidden = false;
        banner.querySelector('[data-consent]')?.focus();
    });

    window.addEventListener('storage', event => {
        if (event.key !== STORAGE_KEY) return;

        apply(parseChoice(event.newValue));
    });

    const choice = readChoice();
    if (choice === null) saveChoice(null); // drop an expired or unreadable answer
    apply(choice);
})();
