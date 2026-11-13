// Turns stylesheets that index.html only preloads (marked data-deferred-style) into real stylesheets, so they download early
// without blocking the first paint. A script file rather than an inline onload handler, which the Content Security Policy blocks.
for (const preload of document.querySelectorAll('link[rel="preload"][as="style"][data-deferred-style]')) {
    const stylesheet = document.createElement('link');
    stylesheet.rel = 'stylesheet';
    stylesheet.href = preload.href;

    // Same integrity, CORS mode and referrer policy as the preload, so the browser reuses the preloaded file.
    for (const attribute of ['integrity', 'crossorigin', 'referrerpolicy']) {
        if (preload.hasAttribute(attribute)) stylesheet.setAttribute(attribute, preload.getAttribute(attribute));
    }

    preload.after(stylesheet);
}
