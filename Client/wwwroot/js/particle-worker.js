import { createScene } from './particle-scene.js';

// Fall back to a 60 Hz timer when worker animation frames are unavailable.
const requestFrame = typeof requestAnimationFrame === 'function'
    ? callback => requestAnimationFrame(callback)
    : callback => setTimeout(() => callback(performance.now()), 1000 / 60);

let scene;

addEventListener('message', ({ data }) => {
    if (data.type === 'start') {
        const { canvas, reduceMotion } = data;
        scene = createScene(canvas, { reduceMotion, requestFrame });
        return;
    }

    scene?.handle(data);
});
