// Checks a published site against its own Content Security Policy, the way a browser would apply it:
//   node .github/scripts/check-published-csp.mjs <publish folder>/wwwroot
// The policy allows the inline import map and the inlined app.css by hash (Client/Csp.targets fills them in at
// publish). A hash that doesn't match, an inline script or style the policy doesn't know, a script or stylesheet from
// a host it doesn't name, or a style or event handler attribute would only show in a visitor's browser, as a page that
// stays blank or unstyled. CI runs this after every
// publish and again after the Prerender project has changed the page. Tests in Tests/js; no packages needed.

import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

// The pages the site serves: index.html, and app-shell.html once the home page is prerendered.
const PAGES = ['index.html', 'app-shell.html'];

// Script elements that hold data rather than code; browsers don't run them, so the policy doesn't apply.
const DATA_TYPES = new Set(['application/json', 'application/ld+json']);

// The source a browser accepts for an inline element: the SHA-256 of its text after the HTML parser has turned every
// CR LF and lone CR into LF.
export function hashSource(text) {
    const normalized = text.replace(/\r\n?/g, '\n');
    return `'sha256-${createHash('sha256').update(normalized, 'utf8').digest('base64')}'`;
}

// "default-src 'self'; script-src 'self' 'sha256-…'" as directive name -> its sources.
export function parsePolicy(policy) {
    const directives = new Map();
    for (const directive of policy.split(';')) {
        const [name, ...sources] = directive.trim().split(/\s+/);
        if (name && !directives.has(name.toLowerCase())) directives.set(name.toLowerCase(), sources);
    }
    return directives;
}

// The value of one attribute in a tag's attribute text, or null. Only the whole name counts: "type" is not "data-type".
export function attribute(attributes, name) {
    const match = new RegExp(`(?:^|\\s)${name}\\s*=\\s*(?:"([^"]*)"|'([^']*)'|([^\\s>]+))`, 'i').exec(attributes);
    return match ? (match[1] ?? match[2] ?? match[3]) : null;
}

// Whether a source list lets the page load the file at an address in its markup. Relative addresses are the site's
// own ('self'). Absolute ones must match a source's scheme and host, "*." matching any subdomain; paths in sources are
// not compared, which only ever lets more through than a browser would.
export function allowsAddress(sources, address) {
    if (!/^([a-z][a-z0-9+.-]*:|\/\/)/i.test(address)) return sources.includes("'self'");

    let url;
    try {
        url = new URL(address, 'https://site.invalid/');
    } catch {
        return false;
    }
    if (url.protocol === 'data:') return sources.includes('data:');

    return sources.some(source => {
        const match = /^(https?):\/\/(\*\.)?([^/:]+)/i.exec(source);
        if (!match || `${match[1].toLowerCase()}:` !== url.protocol) return false;
        const host = match[3].toLowerCase();
        return match[2] ? url.hostname.endsWith(`.${host}`) : url.hostname === host;
    });
}

// What the policy would block on one page, as messages naming the page.
export function pageProblems(html, policy, page) {
    const directives = parsePolicy(policy);
    const scriptSources = directives.get('script-src') ?? directives.get('default-src') ?? [];
    const styleSources = directives.get('style-src') ?? directives.get('default-src') ?? [];
    const problems = [];

    for (const [, attributes, text] of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi)) {
        const src = attribute(attributes, 'src');
        if (src !== null) {
            if (!allowsAddress(scriptSources, src)) problems.push(`${page}: script-src doesn't allow the script ${src}.`);
            continue;
        }
        const type = attribute(attributes, 'type')?.toLowerCase() ?? '';
        if (DATA_TYPES.has(type)) continue;
        if (!scriptSources.includes(hashSource(text))) {
            problems.push(`${page}: an inline script${type ? ` of type ${type}` : ''} has no matching hash in script-src (${hashSource(text)}).`);
        }
    }

    for (const [, text] of html.matchAll(/<style\b[^>]*>([\s\S]*?)<\/style\s*>/gi)) {
        if (!styleSources.includes(hashSource(text))) {
            problems.push(`${page}: an inline style element has no matching hash in style-src (${hashSource(text)}).`);
        }
    }

    // Attributes are checked in the markup alone, without the text of scripts, styles and comments.
    const markup = html
        .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, '')
        .replace(/<!--[\s\S]*?-->/g, '');

    // Stylesheets, and files preloaded as a style or script, which the browser fetches under those directives.
    for (const [, attributes] of markup.matchAll(/<link\b([^>]*)>/gi)) {
        const rel = attribute(attributes, 'rel')?.toLowerCase().split(/\s+/) ?? [];
        const as = attribute(attributes, 'as')?.toLowerCase();
        const href = attribute(attributes, 'href');
        if (!href) continue;
        if (rel.includes('stylesheet') || (rel.includes('preload') && as === 'style')) {
            if (!allowsAddress(styleSources, href)) problems.push(`${page}: style-src doesn't allow the stylesheet ${href}.`);
        } else if (rel.includes('modulepreload') || (rel.includes('preload') && as === 'script')) {
            if (!allowsAddress(scriptSources, href)) problems.push(`${page}: script-src doesn't allow the script ${href}.`);
        }
    }

    for (const [tag] of markup.matchAll(/<[a-z][^>]*>/gi)) {
        if (/\sstyle\s*=/i.test(tag)) problems.push(`${page}: style-src blocks the style attribute in ${tag}`);
        if (/\son[a-z]+\s*=/i.test(tag)) problems.push(`${page}: script-src blocks the event handler attribute in ${tag}`);
    }

    return problems;
}

// Everything wrong with a published wwwroot folder; empty when the policy and the pages agree.
export function siteProblems(wwwroot) {
    const config = JSON.parse(readFileSync(join(wwwroot, 'staticwebapp.config.json'), 'utf8'));
    const policy = config.globalHeaders?.['Content-Security-Policy'];
    if (typeof policy !== 'string') return ['staticwebapp.config.json sends no Content-Security-Policy.'];

    const problems = [];
    for (const placeholder of policy.match(/'sha256-[A-Z-]+-SET-AT-PUBLISH'/g) ?? []) {
        problems.push(`staticwebapp.config.json: the placeholder ${placeholder} was never filled in.`);
    }

    for (const page of PAGES.filter(page => existsSync(join(wwwroot, page)))) {
        problems.push(...pageProblems(readFileSync(join(wwwroot, page), 'utf8'), policy, page));
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
    for (const problem of problems) console.log(`::error::${problem}`);
    console.log(problems.length === 0
        ? `The pages in ${wwwroot} keep to their Content Security Policy.`
        : `${problems.length} problem(s) with the Content Security Policy in ${wwwroot}.`);
    process.exit(problems.length === 0 ? 0 : 1);
}
