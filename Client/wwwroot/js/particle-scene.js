import {
    COLOR, DOT_OPACITY, LINK_DISTANCE, MAX_FPS, PUSH_COUNT,
    addParticles, createParticle, linkOpacity, particleCount, repulse, rescale, step
} from './particle-field.js';

// Draw on either the page canvas or the worker's OffscreenCanvas.
export function createScene(canvas, { reduceMotion = false, requestFrame, random = Math.random }) {
    const context = canvas.getContext('2d');
    const particles = [];
    let width = 0;
    let height = 0;
    let pointer = null;
    let lastTime = null;

    // Keep positions in CSS pixels and scale the canvas backing store for the display.
    function resize({ width: nextWidth, height: nextHeight, ratio }) {
        rescale(particles, width, height, nextWidth, nextHeight);
        width = nextWidth;
        height = nextHeight;
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
        context.setTransform(ratio, 0, 0, ratio, 0, 0);

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
        const elapsed = time - (lastTime ?? time);
        if (lastTime !== null && elapsed < 1000 / MAX_FPS - 1) return;

        lastTime = time;
        const seconds = elapsed / 1000;
        step(particles, seconds, width, height);
        if (pointer) repulse(particles, pointer.x, pointer.y, seconds);
        draw();
    }

    function handle(message) {
        const { type, x, y } = message;
        switch (type) {
            case 'resize':
                resize(message);
                break;
            case 'pointermove':
                pointer = { x, y };
                break;
            case 'pointerleave':
                pointer = null;
                break;
            case 'click':
                addParticles(particles, x, y, PUSH_COUNT, 2 * particleCount(width, height), random);
                break;
        }
    }

    if (!reduceMotion) requestFrame(animate);
    return { handle };
}
