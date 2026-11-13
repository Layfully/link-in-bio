// The particle background behind the pages (tsParticles slim, loaded in index.html).

import { whenLoaded } from '../js/cdn-scripts.js';

// The same look as the earlier particles.js setup: 40 small white dots with faint links, pushed away on hover, more on click.
// With reduced motion the dots stand still and don't react to the pointer: the same picture, without movement.
// tsParticles doesn't check prefers-reduced-motion itself.
function particlesOptions(reduceMotion) {
    return {
        fullScreen: { enable: false },
        // A decorative background gains nothing from 120 frames a second on fast screens; 60 halves the work.
        fpsLimit: 60,
        detectRetina: true,
        particles: {
            // particles.js' value_area 800 meant 800,000 px² per 40 particles; tsParticles takes that area as width × height.
            number: { value: 40, density: { enable: true, width: 1000, height: 800 } },
            color: { value: '#ffffff' },
            shape: { type: 'circle' },
            opacity: { value: 0.2 },
            size: { value: { min: 0.5, max: 2 } },
            links: { enable: true, distance: 150, color: '#ffffff', opacity: 0.2, width: 1 },
            move: { enable: !reduceMotion, speed: 4, direction: 'none', random: false, straight: false, outModes: 'out' }
        },
        interactivity: {
            detectsOn: 'canvas',
            events: {
                onHover: { enable: !reduceMotion, mode: 'repulse' },
                onClick: { enable: !reduceMotion, mode: 'push' },
                resize: { enable: true }
            },
            modes: {
                repulse: { distance: 100, duration: 0.4 },
                push: { quantity: 4 }
            }
        }
    };
}

// The slim preset registers its shapes and interactions once per page load.
let slimLoaded;

// Starts the particle background inside the container and returns a handle whose stop() ends it,
// or null when the page was left before or during the start. The motion setting is read here, so a change applies
// from the next page on.
export async function startParticles(container) {
    if (!container?.isConnected) return null;

    await whenLoaded('tsparticles', () => typeof tsParticles !== 'undefined' && typeof loadSlim !== 'undefined');
    if (!container.isConnected) return null;

    slimLoaded ??= loadSlim(tsParticles);
    await slimLoaded;

    const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

    // The element itself rather than an id lookup, which fails or finds nothing once the page is gone.
    const particles = await tsParticles.load({ id: container.id, element: container, options: particlesOptions(reduceMotion) });

    // Left while loading: nothing would ever stop this animation, so stop it now.
    if (!container.isConnected) {
        particles?.destroy();
        return null;
    }

    return {
        stop() {
            particles?.destroy();
        }
    };
}
