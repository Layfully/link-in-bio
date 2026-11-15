// The particle background behind the pages. index.html loads this module, and it starts itself in the page's
// #particles element, outside Blazor's #app: the dots move from the first paint on every page, whether .NET has
// started or not, and Blazor never replaces them.
//
// The drawing (particle-scene.js) runs in a worker, particle-worker.js, on a canvas handed over with
// transferControlToOffscreen: when the page's main thread is busy, as it is for about a second while .NET starts, the
// dots keep moving instead of freezing and then jumping. Browsers that can't hand a canvas to a worker draw on the page
// as before. Either way this script only tells the scene what happens on the page: size, pointer, clicks.
//
// With reduced motion the dots are drawn once and stand still, without reacting to the pointer. The setting is read
// here, so a change applies from the next page load on.

import { createScene } from './particle-scene.js';

function startParticles(container) {
    const canvas = document.createElement('canvas');
    canvas.className = 'block size-full';
    container.append(canvas);

    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const send = connect(canvas, reduceMotion);

    // The size comes from a ResizeObserver, which reports it after the browser's own layout: measuring the container
    // right after the page changed would make the browser lay it out early (a forced reflow). It also fires once at
    // the start, with the first size; the first dots are placed and drawn then.
    new ResizeObserver(([entry]) => send({
        type: 'resize',
        width: entry.contentRect.width,
        height: entry.contentRect.height,
        ratio: window.devicePixelRatio || 1
    })).observe(container);

    // The canvas sits behind the content, so it only notices the pointer where no content covers it.
    if (!reduceMotion) {
        canvas.addEventListener('pointermove', event => send({ type: 'pointermove', x: event.offsetX, y: event.offsetY }));
        canvas.addEventListener('pointerleave', () => send({ type: 'pointerleave' }));
        canvas.addEventListener('click', event => send({ type: 'click', x: event.offsetX, y: event.offsetY }));
    }
}

// Returns how to tell the scene something: by message to the worker, or by calling it here.
function connect(canvas, reduceMotion) {
    const worker = typeof canvas.transferControlToOffscreen === 'function' ? startModuleWorker() : null;
    if (worker) {
        const offscreen = canvas.transferControlToOffscreen();
        worker.postMessage({ type: 'start', canvas: offscreen, reduceMotion }, [offscreen]);
        return message => worker.postMessage(message);
    }

    const scene = createScene(canvas, { reduceMotion, requestFrame: callback => requestAnimationFrame(callback) });
    return message => scene.handle(message);
}

// The worker, or null where workers can't be modules (Firefox before 114 has OffscreenCanvas but not module workers,
// and would start the worker as a classic script that fails on its import without telling the page). Only a browser
// that supports module workers reads the type option, so reading it is the test.
function startModuleWorker() {
    if (typeof Worker !== 'function') return null;

    let readsType = false;
    const options = { get type() { readsType = true; return 'module'; } };
    const worker = new Worker(new URL('./particle-worker.js', import.meta.url), options);
    if (readsType) return worker;

    worker.terminate();
    return null;
}

const container = document.getElementById('particles');
if (container) startParticles(container);
