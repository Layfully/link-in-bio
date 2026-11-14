// Runs the particle background (particle-scene.js) on its own thread, drawing on the OffscreenCanvas the page hands it.
// The page's main thread can be busy for a second or more, most of all while .NET starts; the dots keep moving
// regardless, because the browser shows this thread's frames without waiting for the page.
// The first message brings the canvas: { type: 'start', canvas, reduceMotion }; every later one goes to the scene.

import { createScene } from './particle-scene.js';

// Workers have requestAnimationFrame in current browsers; elsewhere a timer at 60 frames a second stands in.
const requestFrame = typeof requestAnimationFrame === 'function'
    ? callback => requestAnimationFrame(callback)
    : callback => setTimeout(() => callback(performance.now()), 1000 / 60);

let scene = null;

addEventListener('message', ({ data }) => {
    if (data.type === 'start') {
        scene = createScene(data.canvas, { reduceMotion: data.reduceMotion, requestFrame });
    } else {
        scene?.handle(data);
    }
});
