// Run the background outside Blazor and use a worker where supported.

import { createScene } from './particle-scene.js';

function startParticles(container) {
    const canvas = document.createElement('canvas');
    canvas.className = 'block size-full';
    container.append(canvas);

    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const send = connect(canvas, reduceMotion);

    new ResizeObserver(([{ contentRect: { width, height } }]) => send({
        type: 'resize',
        width,
        height,
        ratio: window.devicePixelRatio || 1
    })).observe(container);

    if (reduceMotion) return;

    const sendPosition = ({ type, offsetX: x, offsetY: y }) => send({ type, x, y });
    canvas.addEventListener('pointermove', sendPosition);
    canvas.addEventListener('click', sendPosition);
    canvas.addEventListener('pointerleave', () => send({ type: 'pointerleave' }));
}

function connect(canvas, reduceMotion) {
    const worker = typeof canvas.transferControlToOffscreen === 'function' ? startModuleWorker() : null;
    if (worker) {
        const offscreen = canvas.transferControlToOffscreen();
        worker.postMessage({ type: 'start', canvas: offscreen, reduceMotion }, [offscreen]);
        return message => worker.postMessage(message);
    }

    const scene = createScene(canvas, { reduceMotion, requestFrame: callback => requestAnimationFrame(callback) });
    return scene.handle;
}

// Some browsers support OffscreenCanvas but ignore the worker's module type.
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
