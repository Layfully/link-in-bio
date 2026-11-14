// The particle background's movement rules, without any drawing: how many dots, how they move, how the pointer pushes
// them and how clicks add more. ParticleBackground.razor.js draws them; Tests/js tests these rules with Node.
// The values reproduce the earlier tsParticles setup (itself the older particles.js one): 40 small pale purple dots
// per 1000 x 800 px with faint links, pushed away on hover, more on click.

export const COLOR = '#e9d5ff'; // purple-200: white with a hint of the accent
export const DOT_OPACITY = 0.25;
export const MIN_RADIUS = 0.5;
export const MAX_RADIUS = 2;
export const LINK_DISTANCE = 150;
export const LINK_OPACITY = 0.12;

// particles.js moved a dot speed / 2 pixels per frame at 60 frames a second; speed was 4.
export const SPEED = 2 * 60; // px per second
export const REPULSE_DISTANCE = 100;
export const REPULSE_DURATION = 0.4; // seconds to push a dot out of the pointer's reach
export const PUSH_COUNT = 4;

// A decorative background gains nothing from 120 frames a second on fast screens.
export const MAX_FPS = 60;

// The longest step one frame may take. Browsers pause animation frames in background tabs; without a limit, the first
// frame back would move every dot by the whole time away.
export const MAX_STEP = 0.1; // seconds

const DENSITY_AREA = 1000 * 800;
const DENSITY_COUNT = 40;

export function particleCount(width, height) {
    return Math.round(DENSITY_COUNT * (width * height) / DENSITY_AREA);
}

// A dot at (x, y) heading in a random direction at the constant speed.
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

// Moves every dot for the time that passed; a dot leaving one edge comes back at the opposite one.
export function step(particles, seconds, width, height) {
    const dt = Math.min(seconds, MAX_STEP);
    for (const p of particles) {
        p.x = wrap(p.x + p.vx * dt, width);
        p.y = wrap(p.y + p.vy * dt, height);
    }
}

// Links fade as dots move apart and vanish at LINK_DISTANCE.
export function linkOpacity(distance) {
    return distance >= LINK_DISTANCE ? 0 : LINK_OPACITY * (1 - distance / LINK_DISTANCE);
}

// Dots within REPULSE_DISTANCE of the pointer move straight away from it, reaching the edge of its reach in
// REPULSE_DURATION. A dot exactly under the pointer has no direction to go and stays.
export function repulse(particles, pointerX, pointerY, seconds) {
    const share = Math.min(1, Math.min(seconds, MAX_STEP) / REPULSE_DURATION);
    for (const p of particles) {
        const dx = p.x - pointerX;
        const dy = p.y - pointerY;
        const distance = Math.hypot(dx, dy);
        if (distance === 0 || distance >= REPULSE_DISTANCE) continue;

        const push = (REPULSE_DISTANCE - distance) * share;
        p.x += (dx / distance) * push;
        p.y += (dy / distance) * push;
    }
}

// Adds dots at the pointer. Beyond `max` the oldest go, so a visitor who keeps clicking can't slow the page down.
export function addParticles(particles, x, y, count, max, random = Math.random) {
    for (let i = 0; i < count; i++) {
        particles.push(createParticle(x, y, random));
    }
    if (particles.length > max) {
        particles.splice(0, particles.length - max);
    }
}

function wrap(value, size) {
    return size > 0 ? ((value % size) + size) % size : value;
}
