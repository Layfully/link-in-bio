// When the page starts Blazor (Client/wwwroot/js/blazor-start.js). Run with: node --test "Tests/js/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const script = readFileSync(new URL('../../Client/wwwroot/js/blazor-start.js', import.meta.url), 'utf8');

// Runs the script on a fake page and returns what it did: how often Blazor started, plus ways to move the page along.
function load({ prerendered, parsed = true, loaded = false, idleCallbacks = true, app = null, preconnects = [] }) {
    const listeners = new Map();
    const idle = [];
    const timers = [];
    const observers = [];
    const page = {
        starts: 0,
        fire(type) {
            if (type === 'DOMContentLoaded') parsed = true;
            if (type === 'load') loaded = true;
            for (const listener of [...(listeners.get(type) ?? [])]) listener();
        },
        runIdle() { for (const callback of idle.splice(0)) callback(); },
        runTimers() { for (const { callback } of timers.splice(0)) callback(); },
        notifyObservers() { for (const observer of [...observers]) observer.callback([], observer); },
        get delays() { return timers.map(timer => timer.delay); }
    };
    const document = {
        body: { tagName: 'BODY' },
        activeElement: null,
        get readyState() { return !parsed ? 'loading' : loaded ? 'complete' : 'interactive'; },
        // Only the links the deploy left without rel, as the script's selector asks for.
        querySelectorAll: () => preconnects.filter(link => link.rel === undefined && link.isConnected),
        getElementById: id => {
            if (!parsed) return null;
            if (id === 'storyblok-prerendered') return prerendered ? {} : null;
            return id === 'app' ? app : null;
        }
    };
    document.activeElement = document.body;
    page.document = document;
    class MutationObserver {
        constructor(callback) { this.callback = callback; }
        observe() { observers.push(this); }
        disconnect() { observers.splice(observers.indexOf(this), 1); }
    }
    const window = {
        Blazor: { start: () => { page.starts++; return Promise.resolve(); } },
        document,
        MutationObserver,
        addEventListener: (type, listener) => listeners.set(type, [...(listeners.get(type) ?? []), listener]),
        removeEventListener: (type, listener) => listeners.set(type, (listeners.get(type) ?? []).filter(l => l !== listener)),
        setTimeout: (callback, delay) => { timers.push({ callback, delay }); }
    };
    if (idleCallbacks) window.requestIdleCallback = callback => { idle.push(callback); };
    vm.runInNewContext(script, { window, ...window });
    return page;
}

// A fake #app holding links. render() swaps them for fresh copies, as Blazor's first render does with the prerendered
// content: the old elements leave the page, and focus on one of them goes back to the body.
function fakeApp(page, hrefs) {
    const focusListeners = [];
    const link = href => {
        const element = {
            isConnected: true,
            getAttribute: name => (name === 'href' ? href : null),
            focus() { page.document.activeElement = element; }
        };
        return element;
    };
    const app = {
        links: hrefs.map(link),
        get childNodes() { return app.links; },
        querySelectorAll: () => app.links,
        addEventListener: (type, listener) => { if (type === 'focusin') focusListeners.push(listener); },
        focus(element) {
            page.document.activeElement = element;
            for (const listener of focusListeners) listener({ target: element });
        },
        render() {
            for (const old of app.links) {
                old.isConnected = false;
                if (page.document.activeElement === old) page.document.activeElement = page.document.body;
            }
            app.links = hrefs.map(link);
        }
    };
    return app;
}

function loadWithLinks(hrefs) {
    const holder = {};
    const app = fakeApp(holder, hrefs);
    const page = load({ prerendered: true, loaded: true, app });
    holder.document = page.document;
    page.app = app;
    page.startBlazor = () => {
        page.runTimers();
        page.runIdle();
        assert.equal(page.starts, 1);
    };
    return page;
}

test('a link that has keyboard focus when Blazor replaces the prerendered content keeps it', () => {
    const page = loadWithLinks(['https://github.com/x', 'https://linkedin.com/x', 'https://example.com']);
    page.app.focus(page.app.links[1]);

    page.startBlazor();
    page.app.render();
    page.notifyObservers();

    assert.equal(page.document.activeElement, page.app.links[1]);
    assert.equal(page.document.activeElement.getAttribute('href'), 'https://linkedin.com/x');
});

test('focus is read when Blazor starts, also when no focus event reached the page', () => {
    // A window in the background gets no focus events, yet its page remembers which link has focus.
    const page = loadWithLinks(['https://github.com/x', 'https://linkedin.com/x']);
    page.document.activeElement = page.app.links[1];

    page.startBlazor();
    page.app.render();
    page.notifyObservers();

    assert.equal(page.document.activeElement, page.app.links[1]);
});

test('a link focused while .NET starts keeps focus too', () => {
    const page = loadWithLinks(['https://github.com/x', 'https://linkedin.com/x']);

    page.startBlazor();
    page.app.focus(page.app.links[0]);
    page.app.render();
    page.notifyObservers();

    assert.equal(page.document.activeElement, page.app.links[0]);
});

test('focus the visitor moved outside the content stays where it is', () => {
    const page = loadWithLinks(['https://github.com/x', 'https://linkedin.com/x']);
    page.app.focus(page.app.links[0]);
    page.startBlazor();
    const consentButton = { tagName: 'BUTTON' };
    page.document.activeElement = consentButton;

    page.app.render();
    page.notifyObservers();

    assert.equal(page.document.activeElement, consentButton);
});

test('changes before Blazor renders leave focus alone, and only the first render counts', () => {
    const page = loadWithLinks(['https://github.com/x', 'https://linkedin.com/x']);
    page.app.focus(page.app.links[0]);
    page.startBlazor();

    page.notifyObservers(); // something else changed; the prerendered content is still there
    assert.equal(page.document.activeElement, page.app.links[0]);

    page.app.render();
    page.notifyObservers();
    page.document.activeElement = page.document.body;
    page.notifyObservers(); // the observer stopped after the first render

    assert.equal(page.document.activeElement, page.document.body);
});

test('a page without prerendered content starts Blazor at once', () => {
    assert.equal(load({ prerendered: false }).starts, 1);
});

test('a page without prerendered content starts as soon as it is parsed', () => {
    const page = load({ prerendered: false, parsed: false });

    assert.equal(page.starts, 0);
    page.fire('DOMContentLoaded');
    assert.equal(page.starts, 1);
});

test('the prerendered home page starts 5 seconds after it has loaded, when the browser is idle', () => {
    const page = load({ prerendered: true });

    page.fire('load');
    assert.deepEqual(page.delays, [5000]);
    page.runIdle();
    assert.equal(page.starts, 0);
    page.runTimers();
    assert.equal(page.starts, 0);
    page.runIdle();
    assert.equal(page.starts, 1);
});

test('a prerendered page that had loaded before the script decided starts when idle', () => {
    const page = load({ prerendered: true, loaded: true });

    page.runTimers();
    page.runIdle();
    assert.equal(page.starts, 1);
});

test('without idle callbacks, as in Safari, a short pause stands in for the idle moment', () => {
    const page = load({ prerendered: true, idleCallbacks: false });

    page.fire('load');
    page.runTimers();
    assert.equal(page.starts, 0);
    assert.deepEqual(page.delays, [100]);
    page.runTimers();
    assert.equal(page.starts, 1);
});

test('a data block further down the page than the script still counts', () => {
    // The prerender step puts the data at the end of the body, after this script: it decides once the page is parsed.
    const page = load({ prerendered: true, parsed: false });

    page.fire('DOMContentLoaded');
    page.runIdle();
    assert.equal(page.starts, 0);
    page.fire('load');
    page.runTimers();
    page.runIdle();
    assert.equal(page.starts, 1);
});

// A link as the deploy leaves it on the prerendered home page: data-preconnect-on-start, no rel.
function deferredPreconnect(href) {
    const link = {
        href,
        isConnected: true,
        replacedWith: null,
        cloneNode: () => ({ href }),
        replaceWith(element) { link.isConnected = false; link.replacedWith = element; }
    };
    return link;
}

test('on the prerendered home page the connection to the API opens when Blazor starts, not at load', () => {
    const link = deferredPreconnect('https://api.storyblok.com/');
    const page = load({ prerendered: true, loaded: true, preconnects: [link] });

    assert.equal(link.replacedWith, null);

    page.runTimers();
    page.runIdle();

    assert.equal(page.starts, 1);
    assert.deepEqual(link.replacedWith, { href: 'https://api.storyblok.com/', rel: 'preconnect' });
});
