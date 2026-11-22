export const COLOR = '#e9d5ff';
export const DOT_OPACITY = 0.25;
export const LINK_DISTANCE = 150;
export const PUSH_COUNT = 4;
export const MAX_FPS = 60;

// Cap movement when animation resumes after a pause.
export const MAX_STEP = 0.1; // seconds

const DENSITY_AREA = 1000 * 800;
const DENSITY_COUNT = 40;
const MIN_RADIUS = 0.5;
const MAX_RADIUS = 2;
const LINK_OPACITY = 0.12;
const SPEED = 120; // px per second
const REPULSE_DISTANCE = 100;
const REPULSE_DURATION = 0.4; // seconds

// Pairwise link checks grow quadratically with the particle count.
export const MAX_PARTICLES = 150;

export function particleCount(width, height) {
    return Math.min(MAX_PARTICLES, Math.round(DENSITY_COUNT * (width * height) / DENSITY_AREA));
}

export function createParticle(x, y, random = Math.random) {
    const angle = random() * 2 * Math.PI;
    return {
        x,
        y,
        vx: Math.cos(angle) * SPEED,
        vy: Math.sin(angle) * SPEED,
        radius: MIN_RADIUS + random() * (MAX_RADIUS - MIN_RADIUS)
    };
}

export function step(particles, seconds, width, height) {
    const dt = Math.min(seconds, MAX_STEP);
    for (const p of particles) {
        p.x = wrap(p.x + p.vx * dt, width);
        p.y = wrap(p.y + p.vy * dt, height);
    }
}

export function linkOpacity(distance) {
    return distance >= LINK_DISTANCE ? 0 : LINK_OPACITY * (1 - distance / LINK_DISTANCE);
}

export function repulse(particles, pointerX, pointerY, seconds) {
    const share = Math.min(seconds, MAX_STEP) / REPULSE_DURATION;
    for (const p of particles) {
        const dx = p.x - pointerX;
        const dy = p.y - pointerY;
        const distance = Math.hypot(dx, dy);
        if (distance === 0 || distance >= REPULSE_DISTANCE) continue;

        const scale = (REPULSE_DISTANCE - distance) * share / distance;
        p.x += dx * scale;
        p.y += dy * scale;
    }
}

// Discard the oldest particles when clicks exceed the limit.
export function addParticles(particles, x, y, count, max, random = Math.random) {
    for (let i = 0; i < count; i++) {
        particles.push(createParticle(x, y, random));
    }
    if (particles.length > max) {
        particles.splice(0, particles.length - max);
    }
}

// Preserve relative positions once the canvas has an initial size.
export function rescale(particles, oldWidth, oldHeight, newWidth, newHeight) {
    if (oldWidth <= 0 || oldHeight <= 0) return;

    const scaleX = newWidth / oldWidth;
    const scaleY = newHeight / oldHeight;
    for (const p of particles) {
        p.x *= scaleX;
        p.y *= scaleY;
    }
}

function wrap(value, size) {
    return size > 0 ? ((value % size) + size) % size : value;
}
