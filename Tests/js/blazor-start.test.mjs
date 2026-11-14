// When the page starts Blazor (Client/wwwroot/js/blazor-start.js). Run with: node --test "Tests/js/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const script = readFileSync(new URL('../../Client/wwwroot/js/blazor-start.js', import.meta.url), 'utf8');

// Runs the script on a fake page and returns what it did: how often Blazor started, plus ways to fire events and timers.
function load({ prerendered, parsed = true }) {
    const listeners = new Map();
    const timers = [];
    const page = {
        starts: 0,
        fire(type) { if (type === 'DOMContentLoaded') parsed = true; for (const listener of [...(listeners.get(type) ?? [])]) listener(); },
        runTimers() { for (const { callback, delay } of timers.splice(0)) callback(delay); }
    };
    const window = {
        Blazor: { start: () => { page.starts++; return Promise.resolve(); } },
        document: {
            get readyState() { return parsed ? 'interactive' : 'loading'; },
            getElementById: id => (parsed && prerendered && id === 'storyblok-prerendered' ? {} : null)
        },
        addEventListener: (type, listener) => listeners.set(type, [...(listeners.get(type) ?? []), listener]),
        removeEventListener: (type, listener) => listeners.set(type, (listeners.get(type) ?? []).filter(l => l !== listener)),
        setTimeout: (callback, delay) => { timers.push({ callback, delay }); page.lastDelay = delay; }
    };
    vm.runInNewContext(script, { window, ...window });
    return page;
}

test('a page without prerendered content starts Blazor at once', () => {
    assert.equal(load({ prerendered: false }).starts, 1);
});

test('the prerendered home page waits for the visitor', () => {
    const page = load({ prerendered: true });

    assert.equal(page.starts, 0);
    page.fire('pointermove');
    assert.equal(page.starts, 1);
});

test('every kind of first interaction starts it, and only once', () => {
    for (const type of ['pointermove', 'pointerdown', 'touchstart', 'wheel', 'scroll', 'keydown', 'focusin']) {
        const page = load({ prerendered: true });
        page.fire(type);
        page.fire('keydown');
        page.fire('scroll');
        assert.equal(page.starts, 1, type);
    }
});

test('without any interaction it starts 5 seconds after the page has loaded', () => {
    const page = load({ prerendered: true });

    page.fire('load');
    assert.equal(page.starts, 0);
    assert.equal(page.lastDelay, 5000);
    page.runTimers();
    assert.equal(page.starts, 1);
});

test('an interaction before the 5 seconds are up means no second start', () => {
    const page = load({ prerendered: true });

    page.fire('load');
    page.fire('pointerdown');
    page.runTimers();
    assert.equal(page.starts, 1);
});

test('a data block further down the page than the script still counts', () => {
    // The prerender step puts the data at the end of the body, after this script: it decides once the page is parsed.
    const page = load({ prerendered: true, parsed: false });

    assert.equal(page.starts, 0);
    page.fire('DOMContentLoaded');
    assert.equal(page.starts, 0);
    page.fire('pointerdown');
    assert.equal(page.starts, 1);
});

test('a page without prerendered content starts as soon as it is parsed', () => {
    const page = load({ prerendered: false, parsed: false });

    page.fire('DOMContentLoaded');
    assert.equal(page.starts, 1);
});
