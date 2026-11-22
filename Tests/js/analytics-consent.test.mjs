// Run with: node --test Tests/js/analytics-consent.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const script = readFileSync(new URL('../../Client/wwwroot/js/analytics-consent.js', import.meta.url), 'utf8');

const MEASUREMENT_ID = 'G-BCKR10P1MJ';
const STORAGE_KEY = 'analytics-consent';
const DAY_MS = 24 * 60 * 60 * 1000;

// Runs the script on a fake page and returns what it did, plus ways to act as the visitor.
function load({ host = 'adriangaborek.dev', stored = null, cookies = '' } = {}) {
    const storage = new Map(stored === null ? [] : [[STORAGE_KEY, stored]]);
    const listeners = { banner: [], settings: [], storage: [] };
    const page = {
        scripts: [],
        cookieWrites: [],
        reloads: 0,
        focused: false,
        storage,
        // A click on one of the banner's buttons ("granted" or "denied").
        answer(consent) {
            const target = { closest: selector => (selector === '[data-consent]' ? { dataset: { consent } } : null) };
            for (const listener of listeners.banner) listener({ target });
        },
        openSettings() { for (const listener of listeners.settings) listener(); },
        // What another tab of the site wrote.
        otherTab(newValue) { for (const listener of listeners.storage) listener({ key: STORAGE_KEY, newValue }); }
    };
    const banner = page.banner = {
        hidden: true,
        addEventListener: (type, listener) => { if (type === 'click') listeners.banner.push(listener); },
        querySelector: () => ({ focus() { page.focused = true; } })
    };
    const settings = page.settings = {
        hidden: false,
        addEventListener: (type, listener) => { if (type === 'click') listeners.settings.push(listener); }
    };
    const document = {
        getElementById: id => ({ 'analytics-consent': banner, 'analytics-settings': settings })[id] ?? null,
        createElement: tagName => ({ tagName }),
        head: { appendChild: element => page.scripts.push(element) },
        get cookie() { return cookies; },
        set cookie(value) { page.cookieWrites.push(value); }
    };
    const window = page.window = {
        document,
        location: { hostname: host, reload() { page.reloads++; } },
        addEventListener: (type, listener) => { if (type === 'storage') listeners.storage.push(listener); }
    };
    const localStorage = {
        getItem: key => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, String(value)),
        removeItem: key => storage.delete(key)
    };
    vm.runInNewContext(script, { window, document, localStorage });
    return page;
}

const answered = (choice, daysAgo = 1) => JSON.stringify({ choice, at: Date.now() - daysAgo * DAY_MS });
const googleScripts = page => page.scripts.filter(element => element.src?.startsWith('https://www.googletagmanager.com/'));

test('other addresses (previews, the editor, localhost) neither ask nor load anything', () => {
    for (const host of ['gray-tree-0c2e88d03.4.azurestaticapps.net', 'localhost']) {
        const page = load({ host, stored: answered('granted') });

        assert.equal(page.banner.hidden, true);
        assert.equal(page.settings.hidden, true);
        assert.deepEqual(googleScripts(page), []);
        assert.equal(page.window.gtag, undefined);
    }
});

test('a first visit asks, and nothing of Google loads before the answer', () => {
    const page = load();

    assert.equal(page.banner.hidden, false);
    assert.deepEqual(googleScripts(page), []);
    assert.equal(page.window.gtag, undefined);
    assert.equal(page.window.dataLayer, undefined);
    assert.equal(page.window[`ga-disable-${MEASUREMENT_ID}`], true);
});

test('Allow loads Analytics, with cookies that last 13 months, and remembers the answer', () => {
    const page = load();

    page.answer('granted');

    assert.equal(page.banner.hidden, true);
    assert.deepEqual(googleScripts(page).map(({ src }) => src), [
        `https://www.googletagmanager.com/gtag/js?id=${MEASUREMENT_ID}`
    ]);
    assert.equal(page.window[`ga-disable-${MEASUREMENT_ID}`], false);
    // Compared as JSON: the script's objects come from its own realm (vm), with prototypes of their own.
    const config = page.window.dataLayer.find(entry => entry[0] === 'config');
    assert.ok(config);
    assert.equal(JSON.stringify([...config]), JSON.stringify(['config', MEASUREMENT_ID, { cookie_expires: 395 * 24 * 60 * 60 }]));
    assert.equal(JSON.parse(page.storage.get(STORAGE_KEY)).choice, 'granted');
});

test('No thanks on a first visit loads nothing and doesn\'t reload', () => {
    const page = load();

    page.answer('denied');

    assert.equal(page.banner.hidden, true);
    assert.deepEqual(googleScripts(page), []);
    assert.equal(page.reloads, 0);
    assert.equal(JSON.parse(page.storage.get(STORAGE_KEY)).choice, 'denied');
});

test('an earlier Allow loads Analytics at once, without asking again', () => {
    const page = load({ stored: answered('granted', 30) });

    assert.equal(page.banner.hidden, true);
    assert.equal(googleScripts(page).length, 1);
});

test('an answer older than 13 months is forgotten and asked again', () => {
    const page = load({ stored: answered('granted', 396) });

    assert.equal(page.banner.hidden, false);
    assert.deepEqual(googleScripts(page), []);
    assert.equal(page.storage.has(STORAGE_KEY), false);
});

test('an unreadable answer counts as none', () => {
    const page = load({ stored: '{"choice":"maybe"' });

    assert.equal(page.banner.hidden, false);
    assert.deepEqual(googleScripts(page), []);
});

test('the settings button asks again without changing anything until the visitor answers', () => {
    const page = load({ stored: answered('granted') });

    page.openSettings();

    assert.equal(page.banner.hidden, false);
    assert.equal(page.focused, true);
    assert.equal(JSON.parse(page.storage.get(STORAGE_KEY)).choice, 'granted');
    assert.equal(page.reloads, 0);
});

test('No thanks after Allow stops Google, removes its cookies on every domain and reloads', () => {
    const page = load({ host: 'www.adriangaborek.dev', stored: answered('granted'), cookies: `_ga=GA1.1.1; theme=dark; _ga_${MEASUREMENT_ID.slice(2)}=GS1` });

    page.openSettings();
    page.answer('denied');

    assert.equal(page.window[`ga-disable-${MEASUREMENT_ID}`], true);
    assert.equal(page.reloads, 1);
    for (const name of ['_ga', `_ga_${MEASUREMENT_ID.slice(2)}`]) {
        assert.ok(page.cookieWrites.includes(`${name}=; Max-Age=0; path=/; domain=www.adriangaborek.dev`));
        assert.ok(page.cookieWrites.includes(`${name}=; Max-Age=0; path=/; domain=adriangaborek.dev`));
        assert.ok(page.cookieWrites.includes(`${name}=; Max-Age=0; path=/`));
    }
    assert.ok(page.cookieWrites.every(write => !write.startsWith('theme=')));
    assert.ok(page.cookieWrites.every(write => !write.includes('domain=dev')));
});

test('an answer given in another tab applies here too', () => {
    const page = load();

    page.otherTab(answered('granted', 0));
    assert.equal(page.banner.hidden, true);
    assert.equal(googleScripts(page).length, 1);

    page.otherTab(null);
    assert.equal(page.banner.hidden, false);
    assert.equal(page.reloads, 1);
});
