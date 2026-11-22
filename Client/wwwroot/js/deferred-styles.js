// Apply preloaded styles asynchronously without an inline handler.
for (const preload of document.querySelectorAll('link[rel="preload"][as="style"][data-deferred-style]')) {
    const stylesheet = preload.cloneNode();
    stylesheet.rel = 'stylesheet';
    stylesheet.removeAttribute('as');
    stylesheet.removeAttribute('data-deferred-style');
    preload.after(stylesheet);
}
