// The check of a published site against its Content Security Policy (.github/scripts/check-published-csp.mjs).
// Run with: node --test "Tests/js/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { hashSource, pageProblems, siteProblems } from '../../.github/scripts/check-published-csp.mjs';

const IMPORT_MAP = '{ "imports": { "./a.js": "./a.abc123.js" } }';
const CSS = 'body{margin:0}';

function page(head = '', body = '') {
    return `<html><head><script type="importmap">${IMPORT_MAP}</script><style>${CSS}</style>${head}</head><body>${body}</body></html>`;
}

function policy({ script = hashSource(IMPORT_MAP), style = hashSource(CSS) } = {}) {
    return `default-src 'self'; script-src 'self' ${script}; style-src 'self' ${style}`;
}

test('a page whose inline import map and style have their hashes passes', () => {
    assert.deepEqual(pageProblems(page(), policy(), 'index.html'), []);
});

test('data blocks and scripts with a src need no hash', () => {
    const html = page(
        '<script type="application/ld+json">{"@type":"Person"}</script><script src="js/a.js"></script>',
        '<script type="application/json" id="storyblok-prerendered">{"home":{}}</script>');

    assert.deepEqual(pageProblems(html, policy(), 'index.html'), []);
});

test('an import map that changed after the hash was taken is reported', () => {
    const problems = pageProblems(page(), policy({ script: hashSource('{}') }), 'index.html');

    assert.equal(problems.length, 1);
    assert.match(problems[0], /^index\.html: an inline script of type importmap/);
});

test('an inline style that changed after the hash was taken is reported', () => {
    assert.match(pageProblems(page(), policy({ style: hashSource('body{}') }), 'index.html')[0], /inline style element/);
});

test('an inline script of any other kind is reported', () => {
    assert.equal(pageProblems(page('<script>alert(1)</script>'), policy(), 'index.html').length, 1);
});

test('line endings are hashed as the browser reads them', () => {
    const html = page().replace(IMPORT_MAP, IMPORT_MAP.replace(' ', '\r\n'));

    assert.deepEqual(pageProblems(html, policy({ script: hashSource(IMPORT_MAP.replace(' ', '\n')) }), 'index.html'), []);
});

test('style and event handler attributes are reported, text that only looks like them is not', () => {
    const html = page('', '<div style="color:red"></div><img src="a.png" onerror="x()"><p>Click on="this"</p><!-- <b style="x"> -->');
    const problems = pageProblems(html, policy(), 'index.html');

    assert.equal(problems.length, 2);
    assert.match(problems[0], /style attribute in <div style="color:red">/);
    assert.match(problems[1], /event handler attribute in <img/);
});

test('a published site is checked page by page, and a placeholder left in the policy is reported', () => {
    const wwwroot = mkdtempSync(join(tmpdir(), 'csp-'));
    try {
        const config = policy({ script: "'sha256-IMPORTMAP-HASH-SET-AT-PUBLISH'" });
        writeFileSync(join(wwwroot, 'staticwebapp.config.json'), JSON.stringify({ globalHeaders: { 'Content-Security-Policy': config } }));
        writeFileSync(join(wwwroot, 'index.html'), page());
        writeFileSync(join(wwwroot, 'app-shell.html'), page());

        const problems = siteProblems(wwwroot);

        assert.equal(problems.length, 3);
        assert.match(problems[0], /IMPORTMAP-HASH-SET-AT-PUBLISH' was never filled in/);
        assert.match(problems[1], /^index\.html:/);
        assert.match(problems[2], /^app-shell\.html:/);
    } finally {
        rmSync(wwwroot, { recursive: true, force: true });
    }
});
