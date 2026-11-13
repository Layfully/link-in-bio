// Libraries that index.html loads from a CDN with defer. Blazor starts without waiting for deferred scripts,
// so on a slow connection a component can render before its library exists.

// Resolves once the library is available: at once if it already is, otherwise when its script tag finishes loading.
// Rejects when the script fails to load. A script that failed before this was called is not detected, so the promise
// then never settles; the effect simply doesn't start, which is the outcome either way.
export function whenLoaded(scriptSrcPart, isAvailable) {
    if (isAvailable()) return Promise.resolve();

    const script = document.querySelector(`script[src*='${scriptSrcPart}']`);
    if (!script) return Promise.reject(new Error(`index.html has no script whose address contains '${scriptSrcPart}'.`));

    return new Promise((resolve, reject) => {
        script.addEventListener('load', () => resolve(), { once: true });
        script.addEventListener('error', () => reject(new Error(`${script.src} could not be loaded.`)), { once: true });
    });
}
