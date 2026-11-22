// Check the final HTML because prerendering can change it after publish computes the CSP hashes.

import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { parse } from 'parse5';

const PAGES = ['index.html', 'app-shell.html'];
const SCRIPT_DATA_TYPES = new Set(['application/json', 'application/ld+json']);
const SITE = 'https://site.invalid';

// HTML parsing normalizes line endings before the browser hashes inline content.
export function hashSource(text) {
    const normalized = text.replace(/\r\n?/g, '\n');
    return `'sha256-${createHash('sha256').update(normalized, 'utf8').digest('base64')}'`;
}

export function parsePolicy(policy) {
    const directives = new Map();
    for (const directive of policy.split(';')) {
        const [name, ...sources] = directive.trim().split(/\s+/);
        const key = name.toLowerCase();
        // CSP uses the first occurrence of each directive.
        if (key && !directives.has(key)) directives.set(key, sources);
    }
    return directives;
}

function* elements(node) {
    if (node.tagName) yield node;
    for (const child of node.childNodes ?? []) yield* elements(child);
}

// For HTTP sources, this checker compares schemes and hostnames only.
export function allowsAddress(sources, address) {
    let url;
    try {
        url = new URL(address, SITE);
    } catch {
        return false;
    }
    if (url.origin === SITE) return sources.includes("'self'");
    if (url.protocol === 'data:') return sources.includes('data:');

    return sources.some(source => {
        const match = /^(https?):\/\/(\*\.)?([^/:]+)/i.exec(source);
        if (!match || `${match[1].toLowerCase()}:` !== url.protocol) return false;
        const host = match[3].toLowerCase();
        return match[2] ? url.hostname.endsWith(`.${host}`) : url.hostname === host;
    });
}

export function pageProblems(html, policy, page) {
    const directives = parsePolicy(policy);
    const defaultSources = directives.get('default-src') ?? [];
    const scriptSources = directives.get('script-src') ?? defaultSources;
    const styleSources = directives.get('style-src') ?? defaultSources;
    const problems = [];

    for (const element of elements(parse(html))) {
        const { tagName, attrs, childNodes } = element;
        const get = (name, prefix) => attrs.find(attr => attr.name === name && attr.prefix === prefix)?.value;
        if (get('style') !== undefined) problems.push(`${page}: style-src blocks the style attribute on <${tagName}>.`);
        if (attrs.some(({ name }) => /^on[a-z]+$/.test(name))) {
            problems.push(`${page}: script-src blocks the event handler attribute on <${tagName}>.`);
        }

        if (tagName === 'script') {
            const src = element.namespaceURI === 'http://www.w3.org/2000/svg'
                ? get('href') ?? get('href', 'xlink')
                : get('src');
            if (src !== undefined) {
                if (!allowsAddress(scriptSources, src)) problems.push(`${page}: script-src doesn't allow the script ${src}.`);
                continue;
            }
            const type = get('type')?.trim().toLowerCase() ?? '';
            if (SCRIPT_DATA_TYPES.has(type)) continue;
            const hash = hashSource(childNodes.map(node => node.value ?? '').join(''));
            if (!scriptSources.includes(hash)) {
                problems.push(`${page}: an inline script${type ? ` of type ${type}` : ''} has no matching hash in script-src (${hash}).`);
            }
        } else if (tagName === 'style') {
            const hash = hashSource(childNodes.map(node => node.value ?? '').join(''));
            if (!styleSources.includes(hash)) {
                problems.push(`${page}: an inline style element has no matching hash in style-src (${hash}).`);
            }
        } else if (tagName === 'link') {
            const rel = get('rel')?.toLowerCase().split(/\s+/) ?? [];
            const as = get('as')?.toLowerCase();
            const href = get('href');
            if (!href) continue;
            const preload = rel.includes('preload');
            if (rel.includes('stylesheet') || (preload && as === 'style')) {
                if (!allowsAddress(styleSources, href)) problems.push(`${page}: style-src doesn't allow the stylesheet ${href}.`);
            } else if (rel.includes('modulepreload') || (preload && as === 'script')) {
                if (!allowsAddress(scriptSources, href)) problems.push(`${page}: script-src doesn't allow the script ${href}.`);
            }
        }
    }

    return problems;
}

export function siteProblems(wwwroot) {
    const config = JSON.parse(readFileSync(join(wwwroot, 'staticwebapp.config.json'), 'utf8'));
    const policy = config.globalHeaders?.['Content-Security-Policy'];
    if (typeof policy !== 'string') return ['staticwebapp.config.json sends no Content-Security-Policy.'];

    const problems = [];
    for (const placeholder of policy.match(/'sha256-[A-Z-]+-SET-AT-PUBLISH'/g) ?? []) {
        problems.push(`staticwebapp.config.json: the placeholder ${placeholder} was never filled in.`);
    }

    for (const page of PAGES) {
        const path = join(wwwroot, page);
        if (existsSync(path)) problems.push(...pageProblems(readFileSync(path, 'utf8'), policy, page));
    }

    return problems;
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
    const wwwroot = process.argv[2];
    if (!wwwroot) {
        console.error('Usage: node .github/scripts/check-published-csp.mjs <published wwwroot folder>');
        process.exit(2);
    }

    const problems = siteProblems(wwwroot);
    for (const problem of problems) {
        console.log(`::error::${problem.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A')}`);
    }
    console.log(problems.length === 0
        ? `The pages in ${wwwroot} keep to their Content Security Policy.`
        : `${problems.length} problem(s) with the Content Security Policy in ${wwwroot}.`);
    process.exit(problems.length === 0 ? 0 : 1);
}
