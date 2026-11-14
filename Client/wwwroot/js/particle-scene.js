// The particle background's drawing and animation, on a canvas it is handed: an OffscreenCanvas in
// particle-worker.js, or the page's own canvas where the browser can't do that (particle-background.js). It knows
// nothing about the page; it learns what happens there through messages:
//   { type: 'resize', width, height, ratio }   the canvas's size in CSS pixels and the screen's pixel ratio
//   { type: 'pointermove', x, y }               the pointer over the canvas, in CSS pixels
//   { type: 'pointerleave' }
//   { type: 'click', x, y }
// The movement rules are in particle-field.js.

import {
    COLOR, DOT_OPACITY, LINK_DISTANCE, MAX_FPS, PUSH_COUNT,
    addParticles, createParticle, linkOpacity, particleCount, repulse, rescale, step
} from './particle-field.js';

// With reduced motion the dots are drawn on every resize and stand still: the same picture, without movement.
export function createScene(canvas, { reduceMotion = false, requestFrame, random = Math.random }) {
    const context = canvas.getContext('2d');
    const particles = [];
    let width = 0;
    let height = 0;
    let pointer = null;
    let lastTime = null;

    // A backing store scaled for sharp dots on high-density screens. The number of dots follows the area; dots added by
    // clicks beyond it go on a resize, and the rest keep their place relative to the canvas.
    function resize(size) {
        rescale(particles, width, height, size.width, size.height);
        ({ width, height } = size);
        canvas.width = Math.round(width * size.ratio);
        canvas.height = Math.round(height * size.ratio);
        context.setTransform(size.ratio, 0, 0, size.ratio, 0, 0);

        const count = particleCount(width, height);
        while (particles.length < count) {
            particles.push(createParticle(random() * width, random() * height, random));
        }
        particles.length = count;
        draw();
    }

    function draw() {
        context.clearRect(0, 0, width, height);
        context.strokeStyle = COLOR;
        context.fillStyle = COLOR;
        context.lineWidth = 1;

        for (let i = 0; i < particles.length; i++) {
            const a = particles[i];
            for (let j = i + 1; j < particles.length; j++) {
                const b = particles[j];
                const distance = Math.hypot(a.x - b.x, a.y - b.y);
                if (distance >= LINK_DISTANCE) continue;

                context.globalAlpha = linkOpacity(distance);
                context.beginPath();
                context.moveTo(a.x, a.y);
                context.lineTo(b.x, b.y);
                context.stroke();
            }
        }

        context.globalAlpha = DOT_OPACITY;
        for (const p of particles) {
            context.beginPath();
            context.arc(p.x, p.y, p.radius, 0, 2 * Math.PI);
            context.fill();
        }
    }

    // Frames pause in background tabs; step() limits how far dots move on the first frame back.
    function animate(time) {
        requestFrame(animate);
        if (lastTime !== null && time - lastTime < 1000 / MAX_FPS - 1) return;

        const seconds = lastTime === null ? 0 : (time - lastTime) / 1000;
        lastTime = time;
        step(particles, seconds, width, height);
        if (pointer) repulse(particles, pointer.x, pointer.y, seconds);
        draw();
    }

    if (!reduceMotion) requestFrame(animate);

    return {
        handle(message) {
            switch (message.type) {
                case 'resize':
                    resize(message);
                    break;
                case 'pointermove':
                    pointer = { x: message.x, y: message.y };
                    break;
                case 'pointerleave':
                    pointer = null;
                    break;
                case 'click':
                    addParticles(particles, message.x, message.y, PUSH_COUNT, 2 * particleCount(width, height), random);
                    break;
            }
        }
    };
}
