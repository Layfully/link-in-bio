// Blazor can render before deferred scripts finish. Earlier script failures are only visible in the browser console.
export async function whenLoaded(scriptSrcPart, isAvailable) {
    if (isAvailable()) return;

    const script = document.querySelector(`script[src*='${scriptSrcPart}']`);
    if (!script) throw new Error(`index.html has no script whose address contains '${scriptSrcPart}'.`);

    return new Promise((resolve, reject) => {
        script.addEventListener('load', () => resolve(), { once: true });
        script.addEventListener('error', () => reject(new Error(`${script.src} could not be loaded.`)), { once: true });
    });
}
