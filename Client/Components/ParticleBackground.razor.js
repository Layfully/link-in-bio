// The particle background behind the pages: dots on a canvas, moved by the rules in js/particle-field.js.
// A few dozen lines instead of tsParticles, whose 160 KB took about a third of a second of script on every visit.

import {
    COLOR, DOT_OPACITY, LINK_DISTANCE, MAX_FPS, PUSH_COUNT,
    addParticles, createParticle, linkOpacity, particleCount, repulse, step
} from '../js/particle-field.js';

// Starts the particle background inside the container and returns a handle whose stop() ends it, or null when the
// container is no longer on the page. With reduced motion the dots are drawn once and stand still, without reacting
// to the pointer: the same picture, without movement. The motion setting is read here, so a change applies from the
// next page load on.
export function startParticles(container) {
    if (!container?.isConnected) return null;

    const canvas = document.createElement('canvas');
    canvas.className = 'block size-full';
    container.append(canvas);

    const context = canvas.getContext('2d');
    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const particles = [];
    let width = 0;
    let height = 0;
    let pointer = null;
    let frame = 0;
    let lastTime = null;

    // Sized to the container in CSS pixels, with a backing store scaled for sharp dots on high-density screens.
    // The number of dots follows the area; dots added by clicks beyond it go on a resize.
    function resize() {
        ({ width, height } = container.getBoundingClientRect());
        const ratio = window.devicePixelRatio || 1;
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
        context.setTransform(ratio, 0, 0, ratio, 0, 0);

        const count = particleCount(width, height);
        while (particles.length < count) {
            particles.push(createParticle(Math.random() * width, Math.random() * height));
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

    // Animation frames stop in background tabs; step() limits how far dots move on the first frame back.
    function animate(time) {
        frame = requestAnimationFrame(animate);
        if (lastTime !== null && time - lastTime < 1000 / MAX_FPS - 1) return;

        const seconds = lastTime === null ? 0 : (time - lastTime) / 1000;
        lastTime = time;
        step(particles, seconds, width, height);
        if (pointer) repulse(particles, pointer.x, pointer.y, seconds);
        draw();
    }

    // The canvas sits behind the content, so it only notices the pointer where no content covers it, as before.
    const onPointerMove = event => { pointer = { x: event.offsetX, y: event.offsetY }; };
    const onPointerLeave = () => { pointer = null; };
    const onClick = event => addParticles(particles, event.offsetX, event.offsetY, PUSH_COUNT, 2 * particleCount(width, height));

    window.addEventListener('resize', resize);
    resize();

    if (!reduceMotion) {
        canvas.addEventListener('pointermove', onPointerMove);
        canvas.addEventListener('pointerleave', onPointerLeave);
        canvas.addEventListener('click', onClick);
        frame = requestAnimationFrame(animate);
    }

    return {
        stop() {
            cancelAnimationFrame(frame);
            window.removeEventListener('resize', resize);
            canvas.remove();
        }
    };
}
