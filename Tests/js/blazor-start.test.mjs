// When the page starts Blazor (Client/wwwroot/js/blazor-start.js). Run with: node --test "Tests/js/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const script = readFileSync(new URL('../../Client/wwwroot/js/blazor-start.js', import.meta.url), 'utf8');

// Runs the script on a fake page and returns what it did: how often Blazor started, plus ways to move the page along.
function load({ prerendered, parsed = true, loaded = false, idleCallbacks = true }) {
    const listeners = new Map();
    const idle = [];
    const timers = [];
    const page = {
        starts: 0,
        fire(type) {
            if (type === 'DOMContentLoaded') parsed = true;
            if (type === 'load') loaded = true;
            for (const listener of [...(listeners.get(type) ?? [])]) listener();
        },
        runIdle() { for (const callback of idle.splice(0)) callback(); },
        runTimers() { for (const callback of timers.splice(0)) callback(); }
    };
    const window = {
        Blazor: { start: () => { page.starts++; return Promise.resolve(); } },
        document: {
            get readyState() { return !parsed ? 'loading' : loaded ? 'complete' : 'interactive'; },
            getElementById: id => (parsed && prerendered && id === 'storyblok-prerendered' ? {} : null)
        },
        addEventListener: (type, listener) => listeners.set(type, [...(listeners.get(type) ?? []), listener]),
        removeEventListener: (type, listener) => listeners.set(type, (listeners.get(type) ?? []).filter(l => l !== listener)),
        setTimeout: callback => { timers.push(callback); }
    };
    if (idleCallbacks) window.requestIdleCallback = callback => { idle.push(callback); };
    vm.runInNewContext(script, { window, ...window });
    return page;
}

test('a page without prerendered content starts Blazor at once', () => {
    assert.equal(load({ prerendered: false }).starts, 1);
});

test('a page without prerendered content starts as soon as it is parsed', () => {
    const page = load({ prerendered: false, parsed: false });

    assert.equal(page.starts, 0);
    page.fire('DOMContentLoaded');
    assert.equal(page.starts, 1);
});

test('the prerendered home page starts once it has loaded and the browser is idle', () => {
    const page = load({ prerendered: true });

    assert.equal(page.starts, 0);
    page.fire('load');
    assert.equal(page.starts, 0);
    page.runIdle();
    assert.equal(page.starts, 1);
});

test('a prerendered page that had loaded before the script decided starts when idle', () => {
    const page = load({ prerendered: true, loaded: true });

    page.runIdle();
    assert.equal(page.starts, 1);
});

test('without idle callbacks, as in Safari, it starts shortly after loading', () => {
    const page = load({ prerendered: true, idleCallbacks: false });

    page.fire('load');
    assert.equal(page.starts, 0);
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
    page.runIdle();
    assert.equal(page.starts, 1);
});
